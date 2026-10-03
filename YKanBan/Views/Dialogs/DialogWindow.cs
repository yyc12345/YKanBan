using Avalonia.Controls;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Views.Dialogs;

/// <summary>
/// Base class of the windows shown by <see cref="DialogService"/>: a standard
/// Avalonia window centered on its owner that closes when its
/// <see cref="DialogViewModelBase"/> asks, with the result as the dialog result.
/// The title bar close button is routed to the view model's cancel path, so a
/// dialog with unsaved changes can ask before discarding them.
/// </summary>
public class DialogWindow : Window
{
    private DialogViewModelBase? _viewModel;
    private bool _closeRequested;
    private bool _cancelPending;

    public DialogWindow()
    {
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;
    }

    /// <summary>Styles the subclasses as plain windows.</summary>
    protected override Type StyleKeyOverride => typeof(Window);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }
        _viewModel = DataContext as DialogViewModelBase;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // Only the user's close button goes through the view model; shutdown and
        // owner closing are let through.
        if (_closeRequested || _viewModel is null || e.CloseReason != WindowCloseReason.WindowClosing)
        {
            return;
        }

        e.Cancel = true;
        if (_cancelPending)
        {
            return;
        }

        _cancelPending = true;
        try
        {
            await _viewModel.RequestCancelAsync();
        }
        finally
        {
            _cancelPending = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        _closeRequested = true;
        Close(result);
    }
}
