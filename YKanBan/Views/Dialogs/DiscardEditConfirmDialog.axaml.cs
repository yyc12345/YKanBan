using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YKanBan.Views.Dialogs;

public partial class DiscardEditConfirmDialog : Window
{
    public DiscardEditConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>Confirms discarding the changes.</summary>
    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    /// <summary>Keeps editing.</summary>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
