using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace YKanBan.Storage.AppData;

/// <summary>
/// Loads and saves app.json with graceful degradation: a missing file yields
/// defaults; a file that is not a JSON object is backed up to app.json.bak and
/// then defaults are used; an individual invalid field (unknown enum value or
/// language, out-of-range width, wrong type) falls back to its own default while
/// the other fields are kept; a file that exists but cannot be read (I/O or
/// permission error) also yields defaults. Saving happens only when this session
/// changed a setting (<see cref="ConfigSection.IsDirty"/>), and writes the file directly (no atomic replace) —
/// losing the last session's settings to a crash is an accepted trade-off.
/// </summary>
public sealed class AppConfigStore
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,

        // Enums round-trip through the kebab-case strings declared via [EnumMember];
        // numeric enum values are rejected like any other unknown value.
        Converters = { new StringEnumConverter { AllowIntegerValues = false } },
    };

    private static readonly JsonSerializer FieldTolerantSerializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        Converters = { new StringEnumConverter { AllowIntegerValues = false } },

        // The JSON text is already known to be well formed here, so every error is a
        // single member that fails to convert: skip it and keep that member's default.
        Error = (_, args) => args.ErrorContext.Handled = true,
    });

    /// <summary>
    /// Initializes a store bound to a specific app.json path.
    /// </summary>
    /// <param name="filePath">Full path of the app.json file to manage.</param>
    public AppConfigStore(string filePath) => FilePath = filePath;

    /// <summary>
    /// Gets the full path of the managed app.json file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Loads the configuration with a clear dirty flag, the baseline for
    /// <see cref="SaveIfDirty"/>. A missing file yields defaults; a file that
    /// is not a JSON object is backed up to app.json.bak (best effort) and then
    /// defaults are used; invalid fields fall back to their defaults
    /// individually; a file that cannot be read yields defaults. Never throws for
    /// file-system reasons, because a bad app.json must not stop the app.
    /// </summary>
    /// <returns>The loaded configuration, or defaults when none could be read.</returns>
    public AppConfig Load()
    {
        AppConfig config = Read();

        // Deserializing and field fallbacks go through the setters; neither is a change by this session.
        config.AcceptChanges();
        return config;
    }

    /// <summary>
    /// Writes the configuration only when a setting was changed since it was
    /// loaded or last saved, even if it was later changed back.
    /// </summary>
    /// <param name="config">The live configuration.</param>
    /// <returns><see langword="true"/> when the file was written.</returns>
    public bool SaveIfDirty(AppConfig config)
    {
        if (!config.IsDirty)
        {
            return false;
        }

        Save(config);
        return true;
    }

    /// <summary>
    /// Writes the configuration directly to disk (no atomic replace),
    /// unconditionally, and clears its dirty flag.
    /// </summary>
    /// <param name="config">The configuration to serialize.</param>
    public void Save(AppConfig config)
    {
        Write(Serialize(config));
        config.AcceptChanges();
    }

    /// <summary>
    /// Reads and sanitizes the file.
    /// </summary>
    /// <returns>The effective configuration.</returns>
    private AppConfig Read()
    {
        // Missing file is a normal first-run state: run with defaults.
        if (!File.Exists(FilePath))
        {
            return new AppConfig();
        }

        string json;
        try
        {
            json = File.ReadAllText(FilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new AppConfig();
        }

        JToken root;
        try
        {
            root = JToken.Parse(json);
        }
        catch (JsonException)
        {
            return BackUpAndUseDefaults();
        }

        // A JSON null literal carries no settings at all; treat it like a missing file.
        if (root.Type == JTokenType.Null)
        {
            return new AppConfig();
        }

        // Well-formed JSON of the wrong shape has no field to keep: handle it like corrupt text.
        if (root is not JObject rootObject)
        {
            return BackUpAndUseDefaults();
        }

        AppConfig config = rootObject.ToObject<AppConfig>(FieldTolerantSerializer) ?? new AppConfig();
        Sanitize(config);
        return config;
    }

    /// <summary>
    /// Keeps a copy of an unusable file for inspection, then continues with
    /// defaults; a backup that cannot be written is skipped.
    /// </summary>
    /// <returns>The default configuration.</returns>
    private AppConfig BackUpAndUseDefaults()
    {
        try
        {
            File.Copy(FilePath, FilePath + ".bak", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The backup is only a diagnostic aid; starting with defaults matters more.
        }
        return new AppConfig();
    }

    /// <summary>
    /// Replaces every value that converted but is outside its domain with the field default.
    /// </summary>
    /// <param name="config">The freshly deserialized configuration.</param>
    private static void Sanitize(AppConfig config)
    {
        var defaults = new AppConfig();

        // Older or hand-edited files may miss whole sections; each falls back to its defaults.
        config.Sort ??= new SortSettings();
        config.ColumnWidth ??= new ColumnWidthSettings();
        config.Confirmations ??= new ConfirmationSettings();

        if (config.Language is null || !AppConfig.SupportedLanguages.Contains(config.Language, StringComparer.Ordinal))
        {
            config.Language = defaults.Language;
        }
        if (!Enum.IsDefined(config.Theme))
        {
            config.Theme = defaults.Theme;
        }
        if (!Enum.IsDefined(config.Sort.Card))
        {
            config.Sort.Card = defaults.Sort.Card;
        }
        if (!Enum.IsDefined(config.ColumnWidth.Preset))
        {
            config.ColumnWidth.Preset = defaults.ColumnWidth.Preset;
        }
        if (config.ColumnWidth.CustomPixels is < AppConfig.MinColumnPixels or > AppConfig.MaxColumnPixels)
        {
            config.ColumnWidth.CustomPixels = defaults.ColumnWidth.CustomPixels;
        }
    }

    /// <summary>
    /// Writes the serialized configuration, creating the parent folder on demand.
    /// </summary>
    /// <param name="json">The serialized configuration.</param>
    private void Write(string json)
    {
        // Normally the folder already exists.
        string? directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, json);
    }

    /// <summary>
    /// Serializes a configuration in the on-disk format.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>The JSON text.</returns>
    private static string Serialize(AppConfig config) => JsonConvert.SerializeObject(config, SerializerSettings);
}
