using CommunityToolkit.Mvvm.Input;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// A purely informational dialog with a single OK button: a localized message
/// plus optional untranslated, copyable diagnostic details.
/// </summary>
public sealed partial class MessageDialogViewModel : DialogViewModelBase
{
    /// <summary>
    /// Initializes the message.
    /// </summary>
    /// <param name="title">The localized window title.</param>
    /// <param name="message">The localized message.</param>
    /// <param name="details">Untranslated diagnostic details, or <see langword="null"/>.</param>
    public MessageDialogViewModel(string title, string message, string? details)
    {
        Title = title;
        Message = message;
        Details = details;
    }

    /// <summary>
    /// Gets the localized window title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the localized message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the diagnostic details, or <see langword="null"/> when there are none.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Gets whether a details block is shown.
    /// </summary>
    public bool HasDetails => !string.IsNullOrEmpty(Details);

    /// <summary>
    /// Closes the dialog.
    /// </summary>
    [RelayCommand]
    private void Ok() => Close(true);
}
