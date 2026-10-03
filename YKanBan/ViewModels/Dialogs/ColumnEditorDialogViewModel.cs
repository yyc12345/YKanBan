using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Models;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The new-column / edit-column dialog: a required title and an optional
/// description. Confirming writes through the supplied save callback; a
/// schema-constraint violation (empty or duplicate title) is shown inside the
/// dialog, which stays open with the user's input intact.
/// </summary>
public sealed partial class ColumnEditorDialogViewModel : DialogViewModelBase
{
    private readonly Action<string, string> _save;

    /// <summary>
    /// Initializes the dialog.
    /// </summary>
    /// <param name="existing">The column being edited, or <see langword="null"/> for a new column.</param>
    /// <param name="save">Writes the title and description; throws the repository's typed exceptions.</param>
    public ColumnEditorDialogViewModel(ColumnModel? existing, Action<string, string> save)
    {
        _save = save;
        Title = existing is null ? Resources.ColumnEditor_TitleNew : Resources.ColumnEditor_TitleEdit;
        columnTitle = existing?.Title ?? "";
        description = existing?.Description ?? "";
    }

    /// <summary>
    /// Gets the localized window title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets or sets the column title being edited (not trimmed).
    /// </summary>
    [ObservableProperty]
    private string columnTitle;

    /// <summary>
    /// Gets or sets the column description being edited.
    /// </summary>
    [ObservableProperty]
    private string description;

    /// <summary>
    /// Gets the localized constraint error of the last confirmation, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    /// <summary>
    /// Gets whether an error is shown.
    /// </summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>
    /// Writes the column; closes on success, otherwise shows the constraint error.
    /// </summary>
    [RelayCommand]
    private void Confirm()
    {
        try
        {
            _save(ColumnTitle, Description);
        }
        catch (Exception exception)
        {
            ErrorMessage = WriteErrorMessages.Describe(exception);
            return;
        }
        Close(true);
    }

    /// <summary>
    /// Closes without writing.
    /// </summary>
    [RelayCommand]
    private Task CancelAsync() => RequestCancelAsync();
}
