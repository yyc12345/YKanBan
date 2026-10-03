using Avalonia.Controls;
using Avalonia.Interactivity;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Views.Dialogs;

public partial class SettingsDialog : Window
{
    private bool _accepted;

    public SettingsDialog()
    {
        InitializeComponent();
    }

    /// <summary>Applies the working settings and closes with a positive result.</summary>
    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as SettingsDialogViewModel)?.Accept();
        _accepted = true;
        Close(true);
    }

    /// <summary>Closes without applying anything.</summary>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    /// <summary>
    /// Restores the previewed theme on every close that did not accept,
    /// including the title-bar close button and Escape.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected override void OnClosed(EventArgs e)
    {
        if (!_accepted)
        {
            (DataContext as SettingsDialogViewModel)?.Cancel();
        }
        base.OnClosed(e);
    }
}
