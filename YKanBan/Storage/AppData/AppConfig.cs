using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace YKanBan.Storage.AppData;

/// <summary>
/// Display ordering options, used for both cards and columns. Except for the id
/// order, every option falls back to id ascending as the final tie-breaker.
/// </summary>
public enum SortOption
{
    /// <summary>Primary key ascending.</summary>
    [EnumMember(Value = "id-asc")]
    IdAscending,

    /// <summary>Primary key descending.</summary>
    [EnumMember(Value = "id-desc")]
    IdDescending,

    /// <summary>Creation time ascending.</summary>
    [EnumMember(Value = "created-asc")]
    CreatedAscending,

    /// <summary>Creation time descending.</summary>
    [EnumMember(Value = "created-desc")]
    CreatedDescending,

    /// <summary>Last modification time ascending.</summary>
    [EnumMember(Value = "updated-asc")]
    UpdatedAscending,

    /// <summary>Last modification time descending.</summary>
    [EnumMember(Value = "updated-desc")]
    UpdatedDescending,

    /// <summary>Title by code points (BINARY collation) ascending.</summary>
    [EnumMember(Value = "title-asc")]
    TitleAscending,

    /// <summary>Title by code points (BINARY collation) descending.</summary>
    [EnumMember(Value = "title-desc")]
    TitleDescending,
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
/// Everything that controls how the board is displayed: the column width and
/// the card and column ordering.
/// </summary>
public sealed class DisplaySettings
{
    /// <summary>
    /// Gets or sets the board column width settings.
    /// </summary>
    [JsonProperty("column-width")]
    public ColumnWidthSettings ColumnWidth { get; set; } = new();

    /// <summary>
    /// Gets or sets the order of cards within their column.
    /// </summary>
    [JsonProperty("card-sort")]
    public SortOption CardSort { get; set; } = SortOption.IdAscending;

    /// <summary>
    /// Gets or sets the left-to-right order of the columns.
    /// </summary>
    [JsonProperty("column-sort")]
    public SortOption ColumnSort { get; set; } = SortOption.IdAscending;
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
    /// Gets or sets the board display settings (width and ordering).
    /// </summary>
    [JsonProperty("display")]
    public DisplaySettings Display { get; set; } = new();

    /// <summary>
    /// Gets or sets the confirmation-dialog settings.
    /// </summary>
    [JsonProperty("confirmations")]
    public ConfirmationSettings Confirmations { get; set; } = new();
}
