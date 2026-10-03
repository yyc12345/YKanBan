using Avalonia.Controls;
using Avalonia.Interactivity;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Views.Dialogs;

public partial class MoveCardDialog : Window
{
    public MoveCardDialog()
    {
        InitializeComponent();
    }

    /// <summary>Closes with the selected destination column id.</summary>
    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MoveCardViewModel viewModel && viewModel.SelectedTarget is not null)
        {
            Close((long)viewModel.SelectedTarget.Value);
        }
    }

    /// <summary>Closes without moving.</summary>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
