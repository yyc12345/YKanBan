using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YKanBan.Views.Dialogs;

public partial class ExportDoneDialog : Window
{
    public ExportDoneDialog()
    {
        InitializeComponent();
    }

    /// <summary>Closes the dialog.</summary>
    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
