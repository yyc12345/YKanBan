namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The confirmation shown before closing an editor with unsaved changes.
/// </summary>
public sealed class DiscardEditConfirmViewModel : ViewModelBase
{
    /// <summary>
    /// Gets the localized confirmation message.
    /// </summary>
    public string Message => Resources.Confirm_Discard_Message;
}
