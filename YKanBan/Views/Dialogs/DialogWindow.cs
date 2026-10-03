using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Views.Dialogs;

/// <summary>
/// Base class of the windows shown by <see cref="DialogService"/>: a standard
/// Avalonia window centered on its owner that closes when its
/// <see cref="DialogViewModelBase"/> asks, with the result as the dialog result.
/// The title bar close button is routed to the view model's cancel path, so a
/// dialog with unsaved changes can ask before discarding them.
/// On opening, the first visible, enabled and editable text box takes the
/// focus with its text selected; dialogs without one leave the focus alone.
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

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Wait for the first layout pass so visibility and enabled state are settled.
        Dispatcher.UIThread.Post(FocusFirstTextBox, DispatcherPriority.Loaded);
    }

    private void FocusFirstTextBox()
    {
        TextBox? target = this.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(box => box.IsEffectivelyVisible && box.IsEffectivelyEnabled && box.Focusable && !box.IsReadOnly);
        if (target is not null && target.Focus())
        {
            target.SelectAll();
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
