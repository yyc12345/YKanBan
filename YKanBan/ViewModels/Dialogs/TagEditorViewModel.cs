using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.Sqlite;
using YKanBan.Models;
using YKanBan.Storage.Workspace;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The add/edit tag dialog: a name, a color chosen with a color picker and an
/// optional description. The write happens when the dialog is accepted; a
/// duplicate or empty name keeps the dialog open with an inline error.
/// </summary>
public sealed partial class TagEditorViewModel : ViewModelBase
{
    private readonly WorkspaceRepository _repository;
    private readonly TagModel? _existing;

    /// <summary>
    /// Initializes the dialog for a new or existing tag.
    /// </summary>
    /// <param name="repository">The workspace repository.</param>
    /// <param name="existing">The tag to edit, or <see langword="null"/> to add one.</param>
    public TagEditorViewModel(WorkspaceRepository repository, TagModel? existing)
    {
        _repository = repository;
        _existing = existing;
        name = existing?.Name ?? string.Empty;
        description = existing?.Description ?? string.Empty;
        color = existing is not null
            ? Color.FromRgb(existing.Color.R, existing.Color.G, existing.Color.B)
            : Colors.Gray;
    }

    /// <summary>
    /// Gets a value indicating whether this dialog creates a new tag.
    /// </summary>
    public bool IsNew => _existing is null;

    /// <summary>
    /// Gets the localized window title.
    /// </summary>
    public string WindowTitle => IsNew ? Resources.TagEditor_AddTitle : Resources.TagEditor_EditTitle;

    /// <summary>
    /// Gets or sets the tag name.
    /// </summary>
    [ObservableProperty]
    private string name;

    /// <summary>
    /// Gets or sets the tag color.
    /// </summary>
    [ObservableProperty]
    private Color color;

    /// <summary>
    /// Gets or sets the tag description.
    /// </summary>
    [ObservableProperty]
    private string description;

    /// <summary>
    /// Gets or sets the inline error shown when the name is rejected.
    /// </summary>
    [ObservableProperty]
    private string? errorMessage;

    /// <summary>
    /// Writes the tag when accepted.
    /// </summary>
    /// <returns><see langword="true"/> when the dialog may close.</returns>
    public bool TryAccept()
    {
        ErrorMessage = null;
        var rgb = new RgbColor(Color.R, Color.G, Color.B);

        try
        {
            if (_existing is null)
            {
                _repository.AddTag(Name, rgb, Description);
            }
            else
            {
                _repository.UpdateTag(_existing.Id, Name, rgb, Description);
            }

            return true;
        }
        catch (SqliteException)
        {
            ErrorMessage = Resources.TagEditor_InvalidName;
            return false;
        }
    }
}
