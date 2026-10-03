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
/// Card ordering settings. The chosen order is global, not per workspace.
/// </summary>
public sealed class SortSettings
{
    /// <summary>
    /// Gets or sets the order used by the board.
    /// </summary>
    [JsonProperty("card")]
    public CardSortOption Card { get; set; } = CardSortOption.Id;
}

/// <summary>
/// Board column width settings, applied globally.
/// </summary>
public sealed class ColumnWidthSettings
{
    /// <summary>
    /// Gets or sets the active width preset.
    /// </summary>
    [JsonProperty("preset")]
    public ColumnWidthPreset Preset { get; set; } = ColumnWidthPreset.Standard;

    /// <summary>
    /// Gets or sets the manual width in pixels, used when <see cref="Preset"/>
    /// is <see cref="ColumnWidthPreset.Custom"/> (range 160–720).
    /// </summary>
    [JsonProperty("custom-pixels")]
    public int CustomPixels { get; set; } = 320;
}

/// <summary>
/// Confirmation-dialog toggles for the destructive actions.
/// </summary>
public sealed class ConfirmationSettings
{
    /// <summary>
    /// Gets or sets whether deleting a card asks for confirmation.
    /// </summary>
    [JsonProperty("delete-card")]
    public bool DeleteCard { get; set; } = true;

    /// <summary>
    /// Gets or sets whether deleting a column asks for confirmation.
    /// </summary>
    [JsonProperty("delete-column")]
    public bool DeleteColumn { get; set; } = true;

    /// <summary>
    /// Gets or sets whether deleting a tag asks for confirmation.
    /// </summary>
    [JsonProperty("delete-tag")]
    public bool DeleteTag { get; set; } = true;

    /// <summary>
    /// Gets or sets whether discarding card edits asks for confirmation.
    /// </summary>
    [JsonProperty("discard-edit")]
    public bool DiscardEdit { get; set; } = true;
}

/// <summary>
/// Root object of app.json, the whole-file key/value configuration store.
/// Keys and enum values are serialized in kebab-case (declared explicitly via
/// <see cref="JsonPropertyAttribute"/> and <see cref="EnumMemberAttribute"/>).
/// The <c>version</c> field is the migration hook for future format changes.
/// </summary>
public sealed class AppConfig
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
    /// Gets or sets the configuration format version of this file.
    /// </summary>
    [JsonProperty("version")]
    public int Version { get; set; } = CurrentFormatVersion;

    /// <summary>
    /// Gets or sets the UI language code ("en" default or "zh-Hans").
    /// </summary>
    [JsonProperty("language")]
    public string Language { get; set; } = DefaultLanguage;

    /// <summary>
    /// Gets or sets the UI theme selection (default: follow the system).
    /// </summary>
    [JsonProperty("theme")]
    public ThemeOption Theme { get; set; } = ThemeOption.FollowSystem;

    /// <summary>
    /// Gets or sets the card ordering settings.
    /// </summary>
    [JsonProperty("sort")]
    public SortSettings Sort { get; set; } = new();

    /// <summary>
    /// Gets or sets the board column width settings.
    /// </summary>
    [JsonProperty("column-width")]
    public ColumnWidthSettings ColumnWidth { get; set; } = new();

    /// <summary>
    /// Gets or sets the confirmation-dialog settings.
    /// </summary>
    [JsonProperty("confirmations")]
    public ConfirmationSettings Confirmations { get; set; } = new();
}
