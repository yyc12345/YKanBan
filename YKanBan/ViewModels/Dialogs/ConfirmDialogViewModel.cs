namespace YKanBan.ViewModels.Dialogs;

/// <summary>A generic confirmation dialog: localized title, message and yes/no answers.</summary>
public sealed class ConfirmDialogViewModel : ViewModelBase
{
    public ConfirmDialogViewModel(string title, string message)
    {
        Title = title;
        Message = message;
        ConfirmText = Resources.Common_Yes;
        CancelText = Resources.Common_No;
    }

    /// <summary>Localized dialog heading.</summary>
    public string Title { get; }

    /// <summary>Localized message body.</summary>
    public string Message { get; }

    /// <summary>Label of the confirming button.</summary>
    public string ConfirmText { get; }

    /// <summary>Label of the cancelling button.</summary>
    public string CancelText { get; }
}
