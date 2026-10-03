using CommunityToolkit.Mvvm.Input;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// A yes/no confirmation: a message, a confirming button with an action
/// specific label (for example "Delete") and a cancel button.
/// </summary>
public sealed partial class ConfirmDialogViewModel : DialogViewModelBase
{
    /// <summary>
    /// Initializes the confirmation.
    /// </summary>
    /// <param name="message">The localized question.</param>
    /// <param name="confirmLabel">The localized label of the confirming button.</param>
    public ConfirmDialogViewModel(string message, string confirmLabel)
    {
        Message = message;
        ConfirmLabel = confirmLabel;
    }

    /// <summary>
    /// Gets the localized question.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the localized label of the confirming button.
    /// </summary>
    public string ConfirmLabel { get; }

    /// <summary>
    /// Closes with a positive result.
    /// </summary>
    [RelayCommand]
    private void Confirm() => Close(true);

    /// <summary>
    /// Closes with a negative result.
    /// </summary>
    [RelayCommand]
    private Task CancelAsync() => RequestCancelAsync();
}
