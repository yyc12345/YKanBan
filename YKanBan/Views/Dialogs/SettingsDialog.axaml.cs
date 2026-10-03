using Avalonia.Controls;
using Avalonia.Interactivity;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Views.Dialogs;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    /// <summary>Applies the working settings and closes with a positive result.</summary>
    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as SettingsDialogViewModel)?.Accept();
        Close(true);
    }

    /// <summary>Closes without applying anything.</summary>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
