using System.Globalization;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The pure-information dialog shown when the search expression is invalid. It
/// wraps the parser's non-localized English detail in the localized message.
/// </summary>
public sealed class SearchErrorViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes the dialog with the parser's English detail.
    /// </summary>
    /// <param name="detail">The non-localized parser error description.</param>
    public SearchErrorViewModel(string detail)
    {
        Message = string.Format(
            Resources.Culture ?? CultureInfo.CurrentCulture,
            Resources.Search_Error_Message,
            detail);
    }

    /// <summary>
    /// Gets the localized error message.
    /// </summary>
    public string Message { get; }
}
