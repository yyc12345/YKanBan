using CommunityToolkit.Mvvm.ComponentModel;

using YKanBan.Storage.AppData;

namespace YKanBan.ViewModels.Board;

/// <summary>
/// The board display options shown in the Display flyout: the global column
/// width and the separate card and column orderings. Every change writes to
/// the configuration and asks the board to refresh immediately.
/// </summary>
public sealed partial class DisplaySettingsViewModel : ViewModelBase {
    /// <summary>
    /// Smallest accepted manual width.
    /// </summary>
    public const int MinimumPixels = 160;

    /// <summary>
    /// Largest accepted manual width.
    /// </summary>
    public const int MaximumPixels = 720;

    private readonly AppConfig _config;
    private readonly Action _refresh;

    /// <summary>
    /// Initializes the display options from the configuration.
    /// </summary>
    /// <param name="config">The live application configuration.</param>
    /// <param name="refresh">Invoked after every change to refresh the board.</param>
    public DisplaySettingsViewModel(AppConfig config, Action refresh) {
        _config = config;
        _refresh = refresh;

        Presets =
        [
            new OptionItem(ColumnWidthPreset.Narrow, Resources.WidthPreset_Narrow),
            new OptionItem(ColumnWidthPreset.Standard, Resources.WidthPreset_Standard),
            new OptionItem(ColumnWidthPreset.Wide, Resources.WidthPreset_Wide),
            new OptionItem(ColumnWidthPreset.Custom, Resources.WidthPreset_Custom),
        ];
        SortOptions = BuildSortOptions();

        selectedPreset = Presets.FirstOrDefault(option => (ColumnWidthPreset)option.Value == config.Display.ColumnWidth.Preset)
            ?? Presets[1];
        customPixels = config.Display.ColumnWidth.CustomPixels;
        selectedCardSort = FindSort(SortOptions, config.Display.CardSort);
        selectedColumnSort = FindSort(SortOptions, config.Display.ColumnSort);
    }

    /// <summary>
    /// Gets the width preset choices.
    /// </summary>
    public IReadOnlyList<OptionItem> Presets { get; }

    /// <summary>
    /// Gets the ordering choices (shared by cards and columns).
    /// </summary>
    public IReadOnlyList<OptionItem> SortOptions { get; }

    /// <summary>
    /// Gets or sets the selected width preset.
    /// </summary>
    [ObservableProperty]
    private OptionItem? selectedPreset;

    /// <summary>
    /// Gets or sets the manual pixel width used by the custom preset.
    /// </summary>
    [ObservableProperty]
    private decimal? customPixels;

    /// <summary>
    /// Gets or sets the selected card order.
    /// </summary>
    [ObservableProperty]
    private OptionItem? selectedCardSort;

    /// <summary>
    /// Gets or sets the selected column order.
    /// </summary>
    [ObservableProperty]
    private OptionItem? selectedColumnSort;

    /// <summary>
    /// Gets a value indicating whether the custom preset is selected.
    /// </summary>
    public bool IsCustom => SelectedPreset is not null && (ColumnWidthPreset)SelectedPreset.Value == ColumnWidthPreset.Custom;

    /// <summary>
    /// Stores the width preset and refreshes the board.
    /// </summary>
    /// <param name="value">The new selection.</param>
    partial void OnSelectedPresetChanged(OptionItem? value) {
        OnPropertyChanged(nameof(IsCustom));
        if (value is not null) {
            _config.Display.ColumnWidth.Preset = (ColumnWidthPreset)value.Value;
            _refresh();
        }
    }

    /// <summary>
    /// Stores the manual width and refreshes the board when the custom preset is active.
    /// </summary>
    /// <param name="value">The new pixel count.</param>
    partial void OnCustomPixelsChanged(decimal? value) {
        if (value is null) {
            return;
        }

        int pixels = (int)value.Value;
        _config.Display.ColumnWidth.CustomPixels = pixels;
        if (IsCustom) {
            _refresh();
        }
    }

    /// <summary>
    /// Stores the card order and refreshes the board.
    /// </summary>
    /// <param name="value">The new selection.</param>
    partial void OnSelectedCardSortChanged(OptionItem? value) {
        if (value is not null) {
            _config.Display.CardSort = (SortOption)value.Value;
            _refresh();
        }
    }

    /// <summary>
    /// Stores the column order and refreshes the board.
    /// </summary>
    /// <param name="value">The new selection.</param>
    partial void OnSelectedColumnSortChanged(OptionItem? value) {
        if (value is not null) {
            _config.Display.ColumnSort = (SortOption)value.Value;
            _refresh();
        }
    }

    /// <summary>
    /// Builds the localized eight-way ordering option list.
    /// </summary>
    /// <returns>The ordering options.</returns>
    private static IReadOnlyList<OptionItem> BuildSortOptions() =>
    [
        new OptionItem(SortOption.IdAscending, Resources.Sort_IdAsc),
        new OptionItem(SortOption.IdDescending, Resources.Sort_IdDesc),
        new OptionItem(SortOption.CreatedAscending, Resources.Sort_CreatedAsc),
        new OptionItem(SortOption.CreatedDescending, Resources.Sort_CreatedDesc),
        new OptionItem(SortOption.UpdatedAscending, Resources.Sort_UpdatedAsc),
        new OptionItem(SortOption.UpdatedDescending, Resources.Sort_UpdatedDesc),
        new OptionItem(SortOption.TitleAscending, Resources.Sort_TitleAsc),
        new OptionItem(SortOption.TitleDescending, Resources.Sort_TitleDesc),
    ];

    /// <summary>
    /// Finds the option matching a stored sort value.
    /// </summary>
    /// <param name="options">The option list.</param>
    /// <param name="value">The stored value.</param>
    /// <returns>The matching option, or the first option.</returns>
    private static OptionItem FindSort(IReadOnlyList<OptionItem> options, SortOption value) =>
        options.FirstOrDefault(option => (SortOption)option.Value == value) ?? options[0];
}
