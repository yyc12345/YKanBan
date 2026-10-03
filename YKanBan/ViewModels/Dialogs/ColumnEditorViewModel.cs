using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.Sqlite;
using YKanBan.Models;
using YKanBan.Storage.Workspace;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The add/edit column dialog: a required title and an optional description.
/// The write happens when the dialog is accepted; a duplicate or empty title
/// keeps the dialog open with an inline error.
/// </summary>
public sealed partial class ColumnEditorViewModel : ViewModelBase
{
    private readonly WorkspaceRepository _repository;
    private readonly ColumnModel? _existing;

    /// <summary>
    /// Initializes the dialog for a new or existing column.
    /// </summary>
    /// <param name="repository">The workspace repository.</param>
    /// <param name="existing">The column to edit, or <see langword="null"/> to add one.</param>
    public ColumnEditorViewModel(WorkspaceRepository repository, ColumnModel? existing)
    {
        _repository = repository;
        _existing = existing;
        title = existing?.Title ?? string.Empty;
        description = existing?.Description ?? string.Empty;
    }

    /// <summary>
    /// Gets a value indicating whether this dialog creates a new column.
    /// </summary>
    public bool IsNew => _existing is null;

    /// <summary>
    /// Gets the localized window title.
    /// </summary>
    public string WindowTitle => IsNew ? Resources.ColumnDialog_AddTitle : Resources.ColumnDialog_EditTitle;

    /// <summary>
    /// Gets or sets the column title.
    /// </summary>
    [ObservableProperty]
    private string title;

    /// <summary>
    /// Gets or sets the column description.
    /// </summary>
    [ObservableProperty]
    private string description;

    /// <summary>
    /// Gets or sets the inline error shown when the title is rejected.
    /// </summary>
    [ObservableProperty]
    private string? errorMessage;

    /// <summary>
    /// Writes the column when accepted.
    /// </summary>
    /// <returns><see langword="true"/> when the dialog may close.</returns>
    public bool TryAccept()
    {
        ErrorMessage = null;
        try
        {
            if (_existing is null)
            {
                _repository.AddColumn(Title, Description);
            }
            else
            {
                _repository.UpdateColumn(_existing.Id, Title, Description);
            }

            return true;
        }
        catch (SqliteException)
        {
            ErrorMessage = Resources.ColumnDialog_InvalidTitle;
            return false;
        }
    }
}
