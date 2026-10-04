using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YKanBan.Views.Dialogs;

public partial class AboutDialog : Window {
    public AboutDialog() {
        InitializeComponent();
    }

    /// <summary>
    /// Closes the dialog.
    /// </summary>
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
