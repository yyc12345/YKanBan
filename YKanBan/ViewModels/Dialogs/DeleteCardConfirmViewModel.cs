using System.Globalization;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The confirmation shown before deleting a card, naming the card id.
/// </summary>
public sealed class DeleteCardConfirmViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes the confirmation.
    /// </summary>
    /// <param name="cardId">The id of the card to delete.</param>
    public DeleteCardConfirmViewModel(long cardId)
    {
        Message = string.Format(
            Resources.Culture ?? CultureInfo.CurrentCulture,
            Resources.Confirm_DeleteCard_Message,
            cardId);
    }

    /// <summary>
    /// Gets the localized confirmation message.
    /// </summary>
    public string Message { get; }
}
