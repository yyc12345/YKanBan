using Avalonia.Controls;
using Avalonia.Interactivity;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Views.Dialogs;

public partial class ColumnEditorDialog : Window
{
    public ColumnEditorDialog()
    {
        InitializeComponent();
    }

    /// <summary>Writes the column and closes when accepted.</summary>
    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ColumnEditorViewModel viewModel && viewModel.TryAccept())
        {
            Close(true);
        }
    }

    /// <summary>Closes without writing.</summary>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
