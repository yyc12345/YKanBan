namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// Opens modal dialogs on behalf of view models, which never reference views.
/// Every dialog is owned by the top-most open window, so a dialog opened from
/// inside another dialog (nested modal) is centered on and blocks that dialog.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a dialog modally and waits until it closes.
    /// </summary>
    /// <param name="dialog">The dialog view model; its window is resolved by naming convention.</param>
    /// <returns>The dialog result: <see langword="true"/> when it was accepted.</returns>
    Task<bool> ShowAsync(DialogViewModelBase dialog);
}

/// <summary>
/// Convenience wrappers for the plain confirmation and message dialogs.
/// </summary>
public static class DialogServiceExtensions
{
    /// <summary>
    /// Asks a yes/no question.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="message">The localized question.</param>
    /// <param name="confirmLabel">The localized label of the confirming button.</param>
    /// <returns><see langword="true"/> when the user confirmed.</returns>
    public static Task<bool> ConfirmAsync(this IDialogService dialogs, string message, string confirmLabel) =>
        dialogs.ShowAsync(new ConfirmDialogViewModel(message, confirmLabel));

    /// <summary>
    /// Shows an informational message with an optional read-only details block.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="title">The localized window title.</param>
    /// <param name="message">The localized message.</param>
    /// <param name="details">Untranslated diagnostic details, or <see langword="null"/>.</param>
    /// <returns>A task completing when the dialog closes.</returns>
    public static Task ShowMessageAsync(this IDialogService dialogs, string title, string message, string? details = null) =>
        dialogs.ShowAsync(new MessageDialogViewModel(title, message, details));
}
