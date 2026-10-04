using Avalonia.Controls;

namespace YKanBan.Views;

/// <summary>
/// Shows a modal window owned by another window, applying the startup culture's
/// flow direction first. FlowDirection does not inherit across windows, so every
/// modal must apply it explicitly.
/// </summary>
internal static class ModalWindow {
    /// <summary>
    /// Shows a modal window that returns a result.
    /// </summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="dialog">The modal window to show.</param>
    /// <typeparam name="TResult">The dialog result type.</typeparam>
    /// <returns>A task completing with the dialog result.</returns>
    public static Task<TResult> ShowAsync<TResult>(Window owner, Window dialog) {
        dialog.FlowDirection = App.UiFlowDirection;
        return dialog.ShowDialog<TResult>(owner);
    }

    /// <summary>
    /// Shows a modal window that has no result.
    /// </summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="dialog">The modal window to show.</param>
    /// <returns>A task completing when the dialog closes.</returns>
    public static Task ShowAsync(Window owner, Window dialog) {
        dialog.FlowDirection = App.UiFlowDirection;
        return dialog.ShowDialog(owner);
    }
}
