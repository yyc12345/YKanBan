using System.Globalization;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The confirmation shown before deleting a column, naming the column and how
/// many cards it will cascade-delete.
/// </summary>
public sealed class DeleteColumnConfirmViewModel : ViewModelBase {
    /// <summary>
    /// Initializes the confirmation.
    /// </summary>
    /// <param name="columnTitle">The title of the column to delete.</param>
    /// <param name="cardCount">The number of cards in the column.</param>
    public DeleteColumnConfirmViewModel(string columnTitle, int cardCount) {
        Message = string.Format(
            Resources.Culture ?? CultureInfo.CurrentCulture,
            Resources.Confirm_DeleteColumn_Message,
            columnTitle,
            cardCount);
    }

    /// <summary>
    /// Gets the localized confirmation message.
    /// </summary>
    public string Message { get; }
}
