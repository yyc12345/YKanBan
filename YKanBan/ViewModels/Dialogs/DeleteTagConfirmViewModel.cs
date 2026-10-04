using System.Globalization;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The confirmation shown before deleting a tag, naming the tag and how many
/// cards it will be removed from.
/// </summary>
public sealed class DeleteTagConfirmViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes the confirmation.
    /// </summary>
    /// <param name="tagName">The name of the tag to delete.</param>
    /// <param name="usageCount">The number of cards using the tag.</param>
    public DeleteTagConfirmViewModel(string tagName, long usageCount)
    {
        Message = string.Format(
            Resources.Culture ?? CultureInfo.CurrentCulture,
            Resources.DeleteTag_Confirm_Message,
            tagName,
            usageCount);
    }

    /// <summary>
    /// Gets the localized confirmation message.
    /// </summary>
    public string Message { get; }
}
