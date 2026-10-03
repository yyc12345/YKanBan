using Avalonia.Controls;
using YKanBan.ViewModels.Dialogs;
using YKanBan.Views.Dialogs;

namespace YKanBan.Views;

/// <summary>
/// Shows dialog view models as modal windows. A <c>XxxDialogViewModel</c> in
/// <c>YKanBan.ViewModels.Dialogs</c> is shown by the <c>XxxDialog</c> window in
/// <c>YKanBan.Views.Dialogs</c>. Each dialog is owned by the top-most open
/// window, so nested modals (the tag picker inside the card editor, a discard
/// confirmation above it) center on their direct parent.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly Stack<Window> _owners = new();

    /// <summary>
    /// Sets the main window as the bottom of the owner stack.
    /// </summary>
    /// <param name="mainWindow">The main window.</param>
    public void Attach(Window mainWindow)
    {
        _owners.Clear();
        _owners.Push(mainWindow);
    }

    /// <inheritdoc/>
    public async Task<bool> ShowAsync(DialogViewModelBase dialog)
    {
        if (!_owners.TryPeek(out Window? owner))
        {
            throw new InvalidOperationException("The dialog service has no main window attached.");
        }

        DialogWindow window = CreateWindow(dialog);
        window.DataContext = dialog;

        // FlowDirection does not inherit across windows, so every modal gets it explicitly.
        window.FlowDirection = App.UiFlowDirection;

        _owners.Push(window);
        try
        {
            return await window.ShowDialog<bool>(owner);
        }
        finally
        {
            _owners.Pop();
        }
    }

    /// <summary>
    /// Creates the window class matching a dialog view model by naming convention.
    /// </summary>
    /// <param name="dialog">The dialog view model.</param>
    /// <returns>The window.</returns>
    private static DialogWindow CreateWindow(DialogViewModelBase dialog)
    {
        string viewName = typeof(ConfirmDialog).Namespace + "." + dialog.GetType().Name.Replace("ViewModel", "", StringComparison.Ordinal);
        Type? viewType = typeof(DialogService).Assembly.GetType(viewName);
        if (viewType is null || !typeof(DialogWindow).IsAssignableFrom(viewType))
        {
            throw new InvalidOperationException($"No dialog window '{viewName}' for {dialog.GetType().Name}.");
        }
        return (DialogWindow)Activator.CreateInstance(viewType)!;
    }
}
