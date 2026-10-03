using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Storage.AppData;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The column width dialog: one of the narrow / standard / wide presets or a
/// manual pixel width (160–720). The choice is global and is committed to the
/// live configuration on confirmation only.
/// </summary>
public sealed partial class ColumnWidthDialogViewModel : DialogViewModelBase
{
    private readonly ColumnWidthSettings _settings;

    /// <summary>
    /// Initializes the dialog from the live settings.
    /// </summary>
    /// <param name="settings">The live column width settings.</param>
    public ColumnWidthDialogViewModel(ColumnWidthSettings settings)
    {
        _settings = settings;
        CultureInfo? culture = Resources.Culture;
        NarrowLabel = string.Format(culture, Resources.ColumnWidth_Narrow, ColumnWidths.Narrow);
        StandardLabel = string.Format(culture, Resources.ColumnWidth_Standard, ColumnWidths.Standard);
        WideLabel = string.Format(culture, Resources.ColumnWidth_Wide, ColumnWidths.Wide);
        RangeLabel = string.Format(culture, Resources.ColumnWidth_Range, AppConfig.MinColumnPixels, AppConfig.MaxColumnPixels);

        isNarrow = settings.Preset == ColumnWidthPreset.Narrow;
        isStandard = settings.Preset == ColumnWidthPreset.Standard;
        isWide = settings.Preset == ColumnWidthPreset.Wide;
        isCustom = settings.Preset == ColumnWidthPreset.Custom;
        customPixels = settings.CustomPixels;
    }

    /// <summary>Gets the localized narrow preset label.</summary>
    public string NarrowLabel { get; }

    /// <summary>Gets the localized standard preset label.</summary>
    public string StandardLabel { get; }

    /// <summary>Gets the localized wide preset label.</summary>
    public string WideLabel { get; }

    /// <summary>Gets the localized accepted pixel range.</summary>
    public string RangeLabel { get; }

    /// <summary>Gets the smallest accepted custom width.</summary>
    public int MinPixels => AppConfig.MinColumnPixels;

    /// <summary>Gets the largest accepted custom width.</summary>
    public int MaxPixels => AppConfig.MaxColumnPixels;

    /// <summary>Gets or sets whether the narrow preset is selected.</summary>
    [ObservableProperty]
    private bool isNarrow;

    /// <summary>Gets or sets whether the standard preset is selected.</summary>
    [ObservableProperty]
    private bool isStandard;

    /// <summary>Gets or sets whether the wide preset is selected.</summary>
    [ObservableProperty]
    private bool isWide;

    /// <summary>Gets or sets whether the manual width is selected.</summary>
    [ObservableProperty]
    private bool isCustom;

    /// <summary>Gets or sets the manual width in pixels; <see langword="null"/> while the box is empty.</summary>
    [ObservableProperty]
    private decimal? customPixels;

    /// <summary>
    /// Commits the selection to the live settings and closes.
    /// </summary>
    [RelayCommand]
    private void Confirm()
    {
        if (IsCustom)
        {
            int pixels = (int)Math.Clamp(CustomPixels ?? _settings.CustomPixels, AppConfig.MinColumnPixels, AppConfig.MaxColumnPixels);
            _settings.Preset = ColumnWidthPreset.Custom;
            _settings.CustomPixels = pixels;
        }
        else
        {
            _settings.Preset = IsNarrow ? ColumnWidthPreset.Narrow
                : IsWide ? ColumnWidthPreset.Wide
                : ColumnWidthPreset.Standard;
        }
        Close(true);
    }

    /// <summary>
    /// Closes without changing anything.
    /// </summary>
    [RelayCommand]
    private Task CancelAsync() => RequestCancelAsync();
}

/// <summary>
/// Pixel widths of the column width presets.
/// </summary>
public static class ColumnWidths
{
    /// <summary>The narrow preset width.</summary>
    public const int Narrow = 240;

    /// <summary>The standard (default) preset width.</summary>
    public const int Standard = 320;

    /// <summary>The wide preset width.</summary>
    public const int Wide = 480;

    /// <summary>
    /// Resolves the effective column width of the settings.
    /// </summary>
    /// <param name="settings">The column width settings.</param>
    /// <returns>The width in pixels.</returns>
    public static int Resolve(ColumnWidthSettings settings) => settings.Preset switch
    {
        ColumnWidthPreset.Narrow => Narrow,
        ColumnWidthPreset.Wide => Wide,
        ColumnWidthPreset.Custom => Math.Clamp(settings.CustomPixels, AppConfig.MinColumnPixels, AppConfig.MaxColumnPixels),
        _ => Standard,
    };
}
