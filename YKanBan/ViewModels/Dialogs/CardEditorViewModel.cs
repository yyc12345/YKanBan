using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using YKanBan.Models;
using YKanBan.Storage.Workspace;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The add/edit card dialog: title, content and the assigned-tag area, plus
/// read-only id and timestamps for an existing card. The confirm writes the
/// card and its tag assignment as one unit; a confirm with no effective change
/// writes nothing; cancelling never writes.
/// </summary>
public sealed partial class CardEditorViewModel : ViewModelBase
{
    private readonly WorkspaceRepository _repository;
    private readonly long _columnId;
    private readonly CardModel? _existing;

    /// <summary>
    /// Initializes the dialog for a new or existing card.
    /// </summary>
    /// <param name="repository">The workspace repository.</param>
    /// <param name="columnId">The owning column (used when adding).</param>
    /// <param name="existing">The card to edit, or <see langword="null"/> to add one.</param>
    public CardEditorViewModel(WorkspaceRepository repository, long columnId, CardModel? existing)
    {
        _repository = repository;
        _columnId = columnId;
        _existing = existing;
        title = existing?.Title ?? string.Empty;
        content = existing?.Content ?? string.Empty;
        AssignedTags = new ObservableCollection<AssignedTagViewModel>(
            (existing?.Tags ?? []).Select(tag => new AssignedTagViewModel(this, tag)));

        if (existing is not null)
        {
            IdLine = string.Format(Culture, Resources.CardDialog_Id, $"#{existing.Id}");
            CreatedLine = string.Format(Culture, Resources.CardDialog_Created, FormatTime(existing.CreatedAt));
            ModifiedLine = string.Format(Culture, Resources.CardDialog_Modified, FormatTime(existing.UpdatedAt));
        }
    }

    /// <summary>
    /// Gets the interaction that opens the nested tag picker.
    /// </summary>
    public Interaction<Unit, TagModel?> ShowTagPicker { get; } = new();

    /// <summary>
    /// Gets the workspace repository (used by the tag picker).
    /// </summary>
    public WorkspaceRepository Repository => _repository;

    /// <summary>
    /// Gets a value indicating whether this dialog creates a new card.
    /// </summary>
    public bool IsNew => _existing is null;

    /// <summary>
    /// Gets the localized window title.
    /// </summary>
    public string WindowTitle => IsNew ? Resources.CardDialog_AddTitle : Resources.CardDialog_EditTitle;

    /// <summary>
    /// Gets the read-only id line, empty for a new card.
    /// </summary>
    public string IdLine { get; } = string.Empty;

    /// <summary>
    /// Gets the read-only created line, empty for a new card.
    /// </summary>
    public string CreatedLine { get; } = string.Empty;

    /// <summary>
    /// Gets the read-only modified line, empty for a new card.
    /// </summary>
    public string ModifiedLine { get; } = string.Empty;

    /// <summary>
    /// Gets the tags currently assigned to the card (in memory until confirmed).
    /// </summary>
    public ObservableCollection<AssignedTagViewModel> AssignedTags { get; }

    /// <summary>
    /// Gets or sets the card title.
    /// </summary>
    [ObservableProperty]
    private string title;

    /// <summary>
    /// Gets or sets the card content.
    /// </summary>
    [ObservableProperty]
    private string content;

    /// <summary>
    /// Gets or sets the inline error shown when the write fails.
    /// </summary>
    [ObservableProperty]
    private string? errorMessage;

    /// <summary>
    /// Gets a value indicating whether there are unsaved effective changes.
    /// </summary>
    public bool HasChanges
    {
        get
        {
            if (_existing is null)
            {
                return Title.Length > 0 || Content.Length > 0 || AssignedTags.Count > 0;
            }

            bool tagsChanged = !AssignedTags.Select(row => row.Tag.Id).ToHashSet()
                .SetEquals(_existing.Tags.Select(tag => tag.Id));
            return _existing.Title != Title || _existing.Content != Content || tagsChanged;
        }
    }

    /// <summary>
    /// Opens the nested tag picker and adds the chosen tag when it is new to the card.
    /// </summary>
    [RelayCommand]
    private async Task AddTagAsync()
    {
        TagModel? tag = await ShowTagPicker.Handle(Unit.Value);
        if (tag is not null && AssignedTags.All(row => row.Tag.Id != tag.Id))
        {
            AssignedTags.Add(new AssignedTagViewModel(this, tag));
        }
    }

    /// <summary>
    /// Removes a tag from the in-memory assignment.
    /// </summary>
    /// <param name="tag">The tag to remove.</param>
    public void RemoveTag(TagModel tag)
    {
        AssignedTagViewModel? row = AssignedTags.FirstOrDefault(item => item.Tag.Id == tag.Id);
        if (row is not null)
        {
            AssignedTags.Remove(row);
        }
    }

    /// <summary>
    /// Writes the card and its tags when there is an effective change.
    /// </summary>
    /// <returns><see langword="true"/> when the dialog may close.</returns>
    public bool TryAccept()
    {
        ErrorMessage = null;

        // A confirm with no effective change writes nothing and just closes.
        if (!HasChanges)
        {
            return true;
        }

        try
        {
            IReadOnlyCollection<long> tagIds = AssignedTags.Select(row => row.Tag.Id).ToList();
            if (_existing is null)
            {
                _repository.AddCard(_columnId, Title, Content, tagIds);
            }
            else
            {
                _repository.UpdateCard(_existing.Id, Title, Content, tagIds);
            }

            return true;
        }
        catch (SqliteException)
        {
            ErrorMessage = Resources.CardDialog_Error;
            return false;
        }
    }

    /// <summary>
    /// Gets the culture used for formatted read-only lines.
    /// </summary>
    private static CultureInfo Culture => Resources.Culture ?? CultureInfo.CurrentCulture;

    /// <summary>
    /// Formats a stored timestamp as a local time using the UI language pattern.
    /// </summary>
    /// <param name="unixSeconds">The stored timestamp.</param>
    /// <returns>The formatted local time.</returns>
    private static string FormatTime(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString(Resources.CardDialog_TimePattern, Culture);
}
