using CommunityToolkit.Mvvm.ComponentModel;

using YKanBan.Storage.AppData;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The settings dialog: language (restart to apply), theme and the four
/// confirmation-dialog toggles. Nothing is applied until the user accepts;
/// cancelling or closing the window discards the changes.
/// </summary>
public sealed partial class SettingsDialogViewModel : ViewModelBase {
    private readonly AppConfig _config;
    private readonly string _startupLanguage;

    /// <summary>
    /// Initializes the dialog from the live configuration.
    /// </summary>
    /// <param name="config">The live application configuration.</param>
    /// <param name="startupLanguage">The language applied at startup, for the restart notice.</param>
    public SettingsDialogViewModel(AppConfig config, string startupLanguage) {
        _config = config;
        _startupLanguage = startupLanguage;

        Languages =
        [
            new OptionItem(AppConfig.DefaultLanguage, Resources.Settings_LanguageEnglish),
            new OptionItem("zh-Hans", Resources.Settings_LanguageChinese),
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
    /// Gets or sets the selected theme; applied when the dialog is accepted.
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
    public void Accept() {
        if (SelectedLanguage is not null) {
            _config.Language = (string)SelectedLanguage.Value;
        }
        if (SelectedTheme is not null) {
            _config.Theme = (ThemeOption)SelectedTheme.Value;
            App.ApplyTheme(_config.Theme);
        }

        _config.Confirmations.DeleteCard = ConfirmDeleteCard;
        _config.Confirmations.DeleteColumn = ConfirmDeleteColumn;
        _config.Confirmations.DeleteTag = ConfirmDeleteTag;
        _config.Confirmations.DiscardEdit = ConfirmDiscardEdit;
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
