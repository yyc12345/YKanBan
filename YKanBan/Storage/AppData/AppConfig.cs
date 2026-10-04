using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace YKanBan.Storage.AppData;

/// <summary>
/// Card ordering options. Every order falls back to id ascending as the final
/// tie-breaker.
/// </summary>
public enum CardSortOption
{
    /// <summary>Primary key ascending (default).</summary>
    [EnumMember(Value = "id")]
    Id,

    /// <summary>Last modification time, most recently modified first.</summary>
    [EnumMember(Value = "updated-at")]
    UpdatedAt,

    /// <summary>Title by code points (BINARY collation); empty titles sort first.</summary>
    [EnumMember(Value = "title")]
    Title,
}

/// <summary>
/// Column width presets. <see cref="Custom"/> uses a user-defined pixel width.
/// </summary>
public enum ColumnWidthPreset
{
    /// <summary>Narrow preset.</summary>
    [EnumMember(Value = "narrow")]
    Narrow,

    /// <summary>Standard preset (320 px, the default).</summary>
    [EnumMember(Value = "standard")]
    Standard,

    /// <summary>Wide preset.</summary>
    [EnumMember(Value = "wide")]
    Wide,

    /// <summary>User-defined pixel width (160–720).</summary>
    [EnumMember(Value = "custom")]
    Custom,
}

/// <summary>
/// UI theme selection.
/// </summary>
public enum ThemeOption
{
    /// <summary>Always light.</summary>
    [EnumMember(Value = "light")]
    Light,

    /// <summary>Always dark.</summary>
    [EnumMember(Value = "dark")]
    Dark,

    /// <summary>Follow the operating system setting (default).</summary>
    [EnumMember(Value = "follow-system")]
    FollowSystem,
}

/// <summary>
/// Base of the app.json sections: records whether any setting was changed
/// since the last load or save (the dirty flag). Assigning the value a setting
/// already holds is not a change; changing a setting and changing it back is.
/// </summary>
public abstract class ConfigSection
{
    private bool _changed;

    /// <summary>
    /// Gets whether a setting of this section, or of a nested section, was changed.
    /// </summary>
    [JsonIgnore]
    public virtual bool IsDirty => _changed;

    /// <summary>
    /// Clears the dirty flag of this section and its nested sections.
    /// </summary>
    public virtual void AcceptChanges() => _changed = false;

    /// <summary>
    /// Stores a setting and marks the section dirty when the value differs.
    /// </summary>
    /// <typeparam name="T">The setting type.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value.</param>
    protected void Set<T>(ref T field, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            _changed = true;
        }
    }
}

/// <summary>
/// Card ordering settings. The chosen order is global, not per workspace.
/// </summary>
public sealed class SortSettings : ConfigSection
{
    /// <summary>
    /// Gets or sets the order used by the board.
    /// </summary>
    [JsonProperty("card")]
    public CardSortOption Card { get; set => Set(ref field, value); } = CardSortOption.Id;
}

/// <summary>
/// Board column width settings, applied globally.
/// </summary>
public sealed class ColumnWidthSettings : ConfigSection
{
    /// <summary>
    /// Gets or sets the active width preset.
    /// </summary>
    [JsonProperty("preset")]
    public ColumnWidthPreset Preset { get; set => Set(ref field, value); } = ColumnWidthPreset.Standard;

    /// <summary>
    /// Gets or sets the manual width in pixels, used when <see cref="Preset"/>
    /// is <see cref="ColumnWidthPreset.Custom"/> (range 160–720).
    /// </summary>
    [JsonProperty("custom-pixels")]
    public int CustomPixels { get; set => Set(ref field, value); } = 320;
}

/// <summary>
/// Confirmation-dialog toggles for the destructive actions.
/// </summary>
public sealed class ConfirmationSettings : ConfigSection
{
    /// <summary>
    /// Gets or sets whether deleting a card asks for confirmation.
    /// </summary>
    [JsonProperty("delete-card")]
    public bool DeleteCard { get; set => Set(ref field, value); } = true;

    /// <summary>
    /// Gets or sets whether deleting a column asks for confirmation.
    /// </summary>
    [JsonProperty("delete-column")]
    public bool DeleteColumn { get; set => Set(ref field, value); } = true;

    /// <summary>
    /// Gets or sets whether deleting a tag asks for confirmation.
    /// </summary>
    [JsonProperty("delete-tag")]
    public bool DeleteTag { get; set => Set(ref field, value); } = true;

    /// <summary>
    /// Gets or sets whether discarding card edits asks for confirmation.
    /// </summary>
    [JsonProperty("discard-edit")]
    public bool DiscardEdit { get; set => Set(ref field, value); } = true;
}

/// <summary>
/// Root object of app.json, the whole-file key/value configuration store.
/// Keys and enum values are serialized in kebab-case (declared explicitly via
/// <see cref="JsonPropertyAttribute"/> and <see cref="EnumMemberAttribute"/>).
/// The <c>version</c> field is the migration hook for future format changes.
/// </summary>
public sealed class AppConfig : ConfigSection
{
    /// <summary>
    /// Current configuration format version.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    /// Default UI language code.
    /// </summary>
    public const string DefaultLanguage = "en";

    /// <summary>
    /// Simplified Chinese UI language code.
    /// </summary>
    public const string SimplifiedChineseLanguage = "zh-Hans";

    /// <summary>
    /// Gets the supported UI language codes; any other stored value falls back to <see cref="DefaultLanguage"/>.
    /// </summary>
    public static IReadOnlyList<string> SupportedLanguages { get; } = [DefaultLanguage, SimplifiedChineseLanguage];

    /// <summary>
    /// Smallest accepted custom column width in pixels.
    /// </summary>
    public const int MinColumnPixels = 160;

    /// <summary>
    /// Largest accepted custom column width in pixels.
    /// </summary>
    public const int MaxColumnPixels = 720;

    /// <summary>
    /// Gets or sets the configuration format version of this file.
    /// </summary>
    [JsonProperty("version")]
    public int Version { get; set => Set(ref field, value); } = CurrentFormatVersion;

    /// <summary>
    /// Gets or sets the UI language code ("en" default or "zh-Hans").
    /// </summary>
    [JsonProperty("language")]
    public string Language { get; set => Set(ref field, value); } = DefaultLanguage;

    /// <summary>
    /// Gets or sets the UI theme selection (default: follow the system).
    /// </summary>
    [JsonProperty("theme")]
    public ThemeOption Theme { get; set => Set(ref field, value); } = ThemeOption.FollowSystem;

    /// <summary>
    /// Gets or sets the card ordering settings.
    /// </summary>
    [JsonProperty("sort")]
    public SortSettings Sort { get; set => Set(ref field, value); } = new();

    /// <summary>
    /// Gets or sets the board column width settings.
    /// </summary>
    [JsonProperty("column-width")]
    public ColumnWidthSettings ColumnWidth { get; set => Set(ref field, value); } = new();

    /// <summary>
    /// Gets or sets the confirmation-dialog settings.
    /// </summary>
    [JsonProperty("confirmations")]
    public ConfirmationSettings Confirmations { get; set => Set(ref field, value); } = new();

    /// <inheritdoc/>
    public override bool IsDirty => base.IsDirty || Sort.IsDirty || ColumnWidth.IsDirty || Confirmations.IsDirty;

    /// <inheritdoc/>
    public override void AcceptChanges()
    {
        base.AcceptChanges();
        Sort.AcceptChanges();
        ColumnWidth.AcceptChanges();
        Confirmations.AcceptChanges();
    }
}
