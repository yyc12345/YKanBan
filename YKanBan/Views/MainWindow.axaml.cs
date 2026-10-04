using Avalonia.Controls;

using YKanBan.ViewModels;
using YKanBan.ViewModels.Dialogs;
using YKanBan.Views.Dialogs;

namespace YKanBan.Views;

public partial class MainWindow : Window {
    private bool _interactionsRegistered;

    public MainWindow() {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel viewModel && !_interactionsRegistered) {
            _interactionsRegistered = true;
            viewModel.ShowSettings.RegisterHandler(ShowSettingsAsync);
            viewModel.ShowAbout.RegisterHandler(ShowAboutAsync);
        }
    }

    /// <summary>
    /// Shows the settings dialog centered on this window.
    /// </summary>
    private async Task ShowSettingsAsync(Unit input) {
        if (App.Services is null) {
            return;
        }

        var dialog = new SettingsDialog {
            DataContext = new SettingsDialogViewModel(App.Services.Config, App.StartupLanguage),
        };
        await ModalWindow.ShowAsync(this, dialog);
    }

    /// <summary>
    /// Shows the about dialog centered on this window.
    /// </summary>
    private async Task ShowAboutAsync(Unit input) {
        var dialog = new AboutDialog {
            DataContext = new AboutDialogViewModel(),
        };
        await ModalWindow.ShowAsync(this, dialog);
    }
}
