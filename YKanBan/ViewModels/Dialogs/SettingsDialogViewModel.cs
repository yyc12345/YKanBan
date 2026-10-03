using CommunityToolkit.Mvvm.ComponentModel;
using YKanBan.Storage.AppData;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The settings dialog: language (restart to apply), theme and the four
/// confirmation-dialog toggles. A theme selection is previewed immediately;
/// everything else waits until the user accepts. Cancelling or closing the
/// window discards the changes and restores the theme the dialog opened with.
/// </summary>
public sealed partial class SettingsDialogViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly string _startupLanguage;
    private readonly Action<ThemeOption> _applyTheme;

    /// <summary>
    /// Initializes the dialog from the live configuration.
    /// </summary>
    /// <param name="config">The live application configuration.</param>
    /// <param name="startupLanguage">The language applied at startup, for the restart notice.</param>
    /// <param name="applyTheme">Applies a theme to the running app; defaults to <see cref="App.ApplyTheme"/>.</param>
    public SettingsDialogViewModel(AppConfig config, string startupLanguage, Action<ThemeOption>? applyTheme = null)
    {
        _config = config;
        _startupLanguage = startupLanguage;
        _applyTheme = applyTheme ?? App.ApplyTheme;

        Languages =
        [
            new OptionItem(AppConfig.DefaultLanguage, Resources.Settings_LanguageEnglish),
            new OptionItem(AppConfig.SimplifiedChineseLanguage, Resources.Settings_LanguageChinese),
        ];
        selectedLanguage = Languages.FirstOrDefault(option => (string)option.Value == _config.Language)
            ?? Languages[0];

        Themes =
        [
            new OptionItem(ThemeOption.Light, Resources.Settings_ThemeLight),
            new OptionItem(ThemeOption.Dark, Resources.Settings_ThemeDark),
            new OptionItem(ThemeOption.FollowSystem, Resources.Settings_ThemeFollowSystem),
        ];
        selectedTheme = Themes.FirstOrDefault(option => option.Value.Equals(_config.Theme)) ?? Themes[^1];

        confirmDeleteCard = _config.Confirmations.DeleteCard;
        confirmDeleteColumn = _config.Confirmations.DeleteColumn;
        confirmDeleteTag = _config.Confirmations.DeleteTag;
        confirmDiscardEdit = _config.Confirmations.DiscardEdit;
        RefreshRestartNotice();
    }

    /// <summary>
    /// Gets the available languages.
    /// </summary>
    public IReadOnlyList<OptionItem> Languages { get; }

    /// <summary>
    /// Gets the available themes.
    /// </summary>
    public IReadOnlyList<OptionItem> Themes { get; }

    /// <summary>
    /// Gets or sets the selected language; takes effect after a restart.
    /// </summary>
    [ObservableProperty]
    private OptionItem? selectedLanguage;

    /// <summary>
    /// Gets or sets the selected theme; applied to the running app as soon as it
    /// changes, and saved to the configuration when the dialog is accepted.
    /// </summary>
    [ObservableProperty]
    private OptionItem? selectedTheme;

    /// <summary>
    /// Gets or sets whether deleting a card asks for confirmation.
    /// </summary>
    [ObservableProperty]
    private bool confirmDeleteCard;

    /// <summary>
    /// Gets or sets whether deleting a column asks for confirmation.
    /// </summary>
    [ObservableProperty]
    private bool confirmDeleteColumn;

    /// <summary>
    /// Gets or sets whether deleting a tag asks for confirmation.
    /// </summary>
    [ObservableProperty]
    private bool confirmDeleteTag;

    /// <summary>
    /// Gets or sets whether discarding card edits asks for confirmation.
    /// </summary>
    [ObservableProperty]
    private bool confirmDiscardEdit;

    /// <summary>
    /// Gets whether the working language differs from the one applied at startup.
    /// </summary>
    [ObservableProperty]
    private bool restartNoticeVisible;

    /// <summary>
    /// Commits the working values into the live configuration.
    /// </summary>
    public void Accept()
    {
        if (SelectedLanguage is not null)
        {
            _config.Language = (string)SelectedLanguage.Value;
        }
        if (SelectedTheme is not null)
        {
            _config.Theme = (ThemeOption)SelectedTheme.Value;
        }

        _config.Confirmations.DeleteCard = ConfirmDeleteCard;
        _config.Confirmations.DeleteColumn = ConfirmDeleteColumn;
        _config.Confirmations.DeleteTag = ConfirmDeleteTag;
        _config.Confirmations.DiscardEdit = ConfirmDiscardEdit;
    }

    /// <summary>
    /// Discards the working values and restores the theme the dialog opened with.
    /// </summary>
    public void Cancel() => _applyTheme(_config.Theme);

    /// <summary>
    /// Previews a theme selection on the running app.
    /// </summary>
    /// <param name="value">The new selection.</param>
    partial void OnSelectedThemeChanged(OptionItem? value)
    {
        if (value is not null)
        {
            _applyTheme((ThemeOption)value.Value);
        }
    }

    /// <summary>
    /// Refreshes the restart notice after a language selection change.
    /// </summary>
    /// <param name="value">The new selection.</param>
    partial void OnSelectedLanguageChanged(OptionItem? value) => RefreshRestartNotice();

    /// <summary>
    /// Recomputes whether the restart notice must be shown.
    /// </summary>
    private void RefreshRestartNotice() =>
        RestartNoticeVisible = SelectedLanguage is not null && (string)SelectedLanguage.Value != _startupLanguage;
}
