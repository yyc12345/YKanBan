using CommunityToolkit.Mvvm.ComponentModel;
using YKanBan.Services;
using YKanBan.Storage.AppData;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The settings dialog: language (restart to apply), theme (applies
/// immediately) and the four confirmation-dialog toggles. All changes write
/// straight into the configuration, which persists at shutdown.
/// </summary>
public partial class SettingsDialogViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly string _startupLanguage;

    public SettingsDialogViewModel(AppServices services)
    {
        _config = services.Config;
        _startupLanguage = App.StartupLanguage;

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
    }

    /// <summary>Available languages.</summary>
    public IReadOnlyList<OptionItem> Languages { get; }

    /// <summary>Available themes.</summary>
    public IReadOnlyList<OptionItem> Themes { get; }

    /// <summary>The selected language; takes effect after a restart.</summary>
    [ObservableProperty]
    private OptionItem? selectedLanguage;

    /// <summary>The selected theme; applies immediately.</summary>
    [ObservableProperty]
    private OptionItem? selectedTheme;

    /// <summary>Whether deleting a card asks for confirmation.</summary>
    [ObservableProperty]
    private bool confirmDeleteCard;

    /// <summary>Whether deleting a column asks for confirmation.</summary>
    [ObservableProperty]
    private bool confirmDeleteColumn;

    /// <summary>Whether deleting a tag asks for confirmation.</summary>
    [ObservableProperty]
    private bool confirmDeleteTag;

    /// <summary>Whether discarding card edits asks for confirmation.</summary>
    [ObservableProperty]
    private bool confirmDiscardEdit;

    /// <summary>True while the selected language differs from the one applied at startup.</summary>
    [ObservableProperty]
    private bool restartNoticeVisible;

    /// <summary>Stores the language and refreshes the restart notice.</summary>
    partial void OnSelectedLanguageChanged(OptionItem? value)
    {
        if (value is null)
        {
            return;
        }
        _config.Language = (string)value.Value;
        RestartNoticeVisible = _config.Language != _startupLanguage;
    }

    /// <summary>Stores the theme and applies it immediately.</summary>
    partial void OnSelectedThemeChanged(OptionItem? value)
    {
        if (value is null)
        {
            return;
        }
        _config.Theme = (ThemeOption)value.Value;
        App.ApplyTheme(_config.Theme);
    }

    partial void OnConfirmDeleteCardChanged(bool value) => _config.Confirmations.DeleteCard = value;

    partial void OnConfirmDeleteColumnChanged(bool value) => _config.Confirmations.DeleteColumn = value;

    partial void OnConfirmDeleteTagChanged(bool value) => _config.Confirmations.DeleteTag = value;

    partial void OnConfirmDiscardEditChanged(bool value) => _config.Confirmations.DiscardEdit = value;
}
