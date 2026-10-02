using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace YKanBan.Storage.AppData;

/// <summary>Card ordering options. All orders fall back to id ascending.</summary>
public enum CardSortOption
{
    /// <summary>Ascending primary key (default).</summary>
    [EnumMember(Value = "id")]
    Id,

    /// <summary>Creation time, oldest first.</summary>
    [EnumMember(Value = "created-at")]
    CreatedAt,

    /// <summary>Last modification time, most recent first.</summary>
    [EnumMember(Value = "updated-at")]
    UpdatedAt,

    /// <summary>Title by code points (BINARY collation); empty titles sort first.</summary>
    [EnumMember(Value = "title")]
    Title,
}

/// <summary>Sidebar workspace list ordering.</summary>
public enum SidebarSortOption
{
    /// <summary>Folder name A to Z.</summary>
    [EnumMember(Value = "name-asc")]
    NameAsc,

    /// <summary>Folder name Z to A.</summary>
    [EnumMember(Value = "name-desc")]
    NameDesc,

    /// <summary>By workspace last edit time, most recent first.</summary>
    [EnumMember(Value = "recently-updated")]
    RecentlyUpdated,
}

/// <summary>Column width presets. Custom uses a user-defined pixel width.</summary>
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

/// <summary>UI theme selection.</summary>
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

/// <summary>Card and sidebar list ordering settings.</summary>
public sealed class SortSettings
{
    /// <summary>Card order used by all workspaces (global, not per workspace).</summary>
    [JsonProperty("card")]
    public CardSortOption Card { get; set; } = CardSortOption.Id;

    /// <summary>Sidebar workspace list order.</summary>
    [JsonProperty("sidebar")]
    public SidebarSortOption Sidebar { get; set; } = SidebarSortOption.NameAsc;
}

/// <summary>Board column width settings.</summary>
public sealed class ColumnWidthSettings
{
    /// <summary>Width preset applied globally.</summary>
    [JsonProperty("preset")]
    public ColumnWidthPreset Preset { get; set; } = ColumnWidthPreset.Standard;

    /// <summary>Manual width in pixels used when <see cref="Preset"/> is Custom (range 160–720).</summary>
    [JsonProperty("custom-pixels")]
    public int CustomPixels { get; set; } = 320;
}

/// <summary>Confirmation-dialog toggles for destructive actions.</summary>
public sealed class ConfirmationSettings
{
    /// <summary>Whether deleting a card asks for confirmation.</summary>
    [JsonProperty("delete-card")]
    public bool DeleteCard { get; set; } = true;

    /// <summary>Whether deleting a column asks for confirmation.</summary>
    [JsonProperty("delete-column")]
    public bool DeleteColumn { get; set; } = true;

    /// <summary>Whether deleting a tag asks for confirmation.</summary>
    [JsonProperty("delete-tag")]
    public bool DeleteTag { get; set; } = true;

    /// <summary>Whether discarding card edits asks for confirmation.</summary>
    [JsonProperty("discard-edit")]
    public bool DiscardEdit { get; set; } = true;
}

/// <summary>
/// Root object of app.json — the trivial KV configuration store. Serialized
/// as a whole with kebab-case names (declared via [JsonProperty] and
/// [EnumMember]); the "version" field is the migration hook for future
/// format changes.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Current configuration format version.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>The default UI language code.</summary>
    public const string DefaultLanguage = "en";

    /// <summary>Configuration format version of this file.</summary>
    [JsonProperty("version")]
    public int Version { get; set; } = CurrentFormatVersion;

    /// <summary>UI language code: "en" (default) or "zh-Hans".</summary>
    [JsonProperty("language")]
    public string Language { get; set; } = DefaultLanguage;

    /// <summary>UI theme selection (default: follow the system).</summary>
    [JsonProperty("theme")]
    public ThemeOption Theme { get; set; } = ThemeOption.FollowSystem;

    /// <summary>Ordering settings.</summary>
    [JsonProperty("sort")]
    public SortSettings Sort { get; set; } = new();

    /// <summary>Column width settings.</summary>
    [JsonProperty("column-width")]
    public ColumnWidthSettings ColumnWidth { get; set; } = new();

    /// <summary>Confirmation-dialog settings.</summary>
    [JsonProperty("confirmations")]
    public ConfirmationSettings Confirmations { get; set; } = new();
}
