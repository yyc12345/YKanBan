using Avalonia.Controls;
using Avalonia.Interactivity;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Views.Dialogs;

public partial class TagPickerDialog : Window
{
    public TagPickerDialog()
    {
        InitializeComponent();
    }

    /// <summary>Closes with the selected tag.</summary>
    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TagPickerViewModel viewModel && viewModel.SelectedTarget is { } badge)
        {
            Close(badge.Tag);
        }
    }

    /// <summary>Closes without picking a tag.</summary>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
