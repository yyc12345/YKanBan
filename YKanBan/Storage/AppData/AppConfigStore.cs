using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace YKanBan.Storage.AppData;

/// <summary>
/// Loads and saves app.json with graceful degradation: a missing file yields
/// defaults; a corrupt file is backed up to app.json.bak and then defaults
/// are used. Saving writes the file directly (no atomic replace) — losing
/// the last session's settings to a crash is an accepted trade-off.
/// </summary>
public sealed class AppConfigStore
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,

        // Enums round-trip through the kebab-case strings declared via [EnumMember].
        Converters = { new StringEnumConverter() },
    };

    /// <summary>Creates a store bound to a specific app.json path.</summary>
    public AppConfigStore(string filePath) => FilePath = filePath;

    /// <summary>Full path of the managed app.json file.</summary>
    public string FilePath { get; }

    /// <summary>Loads the configuration: missing file → defaults; corrupt file → backup then defaults.</summary>
    /// <exception cref="IOException">The file exists but cannot be read.</exception>
    public AppConfig Load()
    {
        // Missing file is a normal first-run state: run with defaults.
        if (!File.Exists(FilePath))
        {
            return new AppConfig();
        }

        string json = File.ReadAllText(FilePath);
        try
        {
            // A JSON null literal deserializes to null; treat it like defaults.
            AppConfig? config = JsonConvert.DeserializeObject<AppConfig>(json, SerializerSettings);
            if (config is null)
            {
                return new AppConfig();
            }

            // Older or hand-edited files may miss whole sections; each falls back to its defaults.
            config.Sort ??= new SortSettings();
            config.ColumnWidth ??= new ColumnWidthSettings();
            config.Confirmations ??= new ConfirmationSettings();
            return config;
        }
        catch (JsonException)
        {
            // Corrupt: keep a copy for inspection, then continue with defaults.
            File.Copy(FilePath, FilePath + ".bak", overwrite: true);
            return new AppConfig();
        }
    }

    /// <summary>Writes the configuration directly to disk (no atomic replace).</summary>
    public void Save(AppConfig config)
    {
        // Create the parent folder on demand; normally it exists already.
        string? directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(FilePath, JsonConvert.SerializeObject(config, SerializerSettings));
    }
}
