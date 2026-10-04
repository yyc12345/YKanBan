using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YKanBan.Views.Dialogs;

public partial class DeleteTagConfirmDialog : Window
{
    public DeleteTagConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>Confirms the deletion.</summary>
    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    /// <summary>Cancels the deletion.</summary>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
