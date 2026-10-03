namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// Base class for modal dialog view models shown through <see cref="IDialogService"/>.
/// The dialog decides when it closes: it raises <see cref="CloseRequested"/>
/// with its result, and a close attempted by the window itself (title-bar close
/// button, Escape) is routed to <see cref="RequestCancelAsync"/> so that the
/// dialog can veto it, for example to ask before discarding edits.
/// </summary>
public abstract class DialogViewModelBase : ViewModelBase
{
    /// <summary>
    /// Raised when the dialog wants to close; the argument is the dialog result.
    /// </summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>
    /// Handles a cancel request coming from the window (or a cancel button).
    /// The default closes immediately with a negative result.
    /// </summary>
    /// <returns>A task completing once the request has been handled.</returns>
    public virtual Task RequestCancelAsync()
    {
        Close(false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Asks the hosting window to close with the given result.
    /// </summary>
    /// <param name="result">The dialog result.</param>
    protected void Close(bool result) => CloseRequested?.Invoke(this, result);
}
