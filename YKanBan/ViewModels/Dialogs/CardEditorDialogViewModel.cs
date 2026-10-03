using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Models;
using YKanBan.Storage.Workspace;
using YKanBan.ViewModels.Board;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The card editor — one unit of work. Title, content and the tag assignment
/// are edited in memory and written together in a single transaction on
/// confirmation; cancelling writes nothing about the card or its assignment.
/// Confirming an existing card without effective changes writes nothing and
/// keeps its timestamps. The only immediate write is the creation of a new tag
/// in the nested tag picker (see <see cref="TagPickerDialogViewModel"/>).
/// </summary>
public sealed partial class CardEditorDialogViewModel : DialogViewModelBase
{
    private readonly WorkspaceRepository _repository;
    private readonly IDialogService _dialogs;
    private readonly CardModel? _existing;
    private readonly long _columnId;
    private readonly bool _confirmDiscard;
    private readonly string _initialTitle;
    private readonly string _initialContent;
    private readonly HashSet<long> _initialTagIds;

    /// <summary>
    /// Initializes the editor for a new card in a column.
    /// </summary>
    /// <param name="repository">The workspace repository.</param>
    /// <param name="dialogs">The dialog service, for the nested tag picker and the discard confirmation.</param>
    /// <param name="columnId">The column receiving the new card.</param>
    /// <param name="confirmDiscard">Whether discarding changes asks for confirmation.</param>
    /// <returns>The editor.</returns>
    public static CardEditorDialogViewModel ForNewCard(
        WorkspaceRepository repository, IDialogService dialogs, long columnId, bool confirmDiscard) =>
        new(repository, dialogs, null, columnId, confirmDiscard);

    /// <summary>
    /// Initializes the editor for an existing card.
    /// </summary>
    /// <param name="repository">The workspace repository.</param>
    /// <param name="dialogs">The dialog service, for the nested tag picker and the discard confirmation.</param>
    /// <param name="card">The card to edit.</param>
    /// <param name="confirmDiscard">Whether discarding changes asks for confirmation.</param>
    /// <returns>The editor.</returns>
    public static CardEditorDialogViewModel ForExistingCard(
        WorkspaceRepository repository, IDialogService dialogs, CardModel card, bool confirmDiscard) =>
        new(repository, dialogs, card, card.ColumnId, confirmDiscard);

    private CardEditorDialogViewModel(
        WorkspaceRepository repository, IDialogService dialogs, CardModel? existing, long columnId, bool confirmDiscard)
    {
        _repository = repository;
        _dialogs = dialogs;
        _existing = existing;
        _columnId = columnId;
        _confirmDiscard = confirmDiscard;
        _initialTitle = existing?.Title ?? "";
        _initialContent = existing?.Content ?? "";
        _initialTagIds = existing?.Tags.Select(tag => tag.Id).ToHashSet() ?? [];

        cardTitle = _initialTitle;
        content = _initialContent;
        Tags = new ObservableCollection<TagBadgeViewModel>(
            existing?.Tags.Select(tag => new TagBadgeViewModel(tag)) ?? []);

        if (existing is null)
        {
            Title = Resources.CardEditor_TitleNew;
        }
        else
        {
            CultureInfo? culture = Resources.Culture;
            Title = Resources.CardEditor_TitleEdit;
            IdText = $"#{existing.Id.ToString(CultureInfo.InvariantCulture)}";
            CreatedText = string.Format(culture, Resources.CardEditor_Created, FormatTime(existing.CreatedAt));
            ModifiedText = string.Format(culture, Resources.CardEditor_Modified, FormatTime(existing.UpdatedAt));
        }
    }

    /// <summary>
    /// Gets the localized window title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets whether the card already exists (and thus has an id and timestamps to show).
    /// </summary>
    public bool IsExisting => _existing is not null;

    /// <summary>
    /// Gets the read-only "#id" text of an existing card.
    /// </summary>
    public string? IdText { get; }

    /// <summary>
    /// Gets the read-only creation time line of an existing card.
    /// </summary>
    public string? CreatedText { get; }

    /// <summary>
    /// Gets the read-only modification time line of an existing card.
    /// </summary>
    public string? ModifiedText { get; }

    /// <summary>
    /// Gets the tags assigned in this edit session, in assignment order.
    /// </summary>
    public ObservableCollection<TagBadgeViewModel> Tags { get; }

    /// <summary>
    /// Gets the card written on confirmation; <see langword="null"/> until then.
    /// </summary>
    public CardModel? SavedCard { get; private set; }

    /// <summary>
    /// Gets or sets the card title; the empty string means "no title".
    /// </summary>
    [ObservableProperty]
    private string cardTitle;

    /// <summary>
    /// Gets or sets the multiline card content.
    /// </summary>
    [ObservableProperty]
    private string content;

    /// <summary>
    /// Gets the localized error of the last confirmation, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    /// <summary>
    /// Gets whether an error is shown.
    /// </summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>
    /// Gets whether the in-memory state differs from the state the editor opened with.
    /// </summary>
    public bool IsDirty =>
        CardTitle != _initialTitle
        || Content != _initialContent
        || !_initialTagIds.SetEquals(Tags.Select(tag => tag.Tag.Id));

    /// <summary>
    /// Opens the nested tag picker and assigns the picked (or newly created) tag.
    /// </summary>
    /// <returns>A task completing when the picker closes.</returns>
    [RelayCommand]
    private async Task AddTagAsync()
    {
        TagPickerDialogViewModel picker;
        try
        {
            picker = new TagPickerDialogViewModel(_repository, Tags.Select(tag => tag.Tag.Id).ToHashSet());
        }
        catch (Exception exception)
        {
            // The picker lists the tags as it opens; a read failure must not escape the command.
            await _dialogs.ShowReadFailedAsync(exception);
            return;
        }

        if (await _dialogs.ShowAsync(picker) && picker.PickedTag is { } picked)
        {
            Tags.Add(new TagBadgeViewModel(picked));
        }
    }

    /// <summary>
    /// Removes a tag from the in-memory assignment.
    /// </summary>
    /// <param name="tag">The tag to remove.</param>
    [RelayCommand]
    private void RemoveTag(TagBadgeViewModel tag) => Tags.Remove(tag);

    /// <summary>
    /// Writes the card in one transaction and closes; an unchanged existing card
    /// is not written at all.
    /// </summary>
    [RelayCommand]
    private void Confirm()
    {
        if (_existing is not null && !IsDirty)
        {
            SavedCard = _existing;
            Close(true);
            return;
        }

        long[] tagIds = Tags.Select(tag => tag.Tag.Id).ToArray();
        try
        {
            SavedCard = _existing is null
                ? _repository.AddCard(_columnId, CardTitle, Content, tagIds)
                : _repository.UpdateCard(_existing.Id, CardTitle, Content, tagIds);
        }
        catch (Exception exception)
        {
            ErrorMessage = WriteErrorMessages.Describe(exception);
            return;
        }
        Close(true);
    }

    /// <summary>
    /// Closes without writing the card, asking first when there are unsaved
    /// changes and the discard confirmation is enabled.
    /// </summary>
    [RelayCommand]
    private Task CancelAsync() => RequestCancelAsync();

    /// <inheritdoc/>
    public override async Task RequestCancelAsync()
    {
        if (_confirmDiscard && IsDirty
            && !await _dialogs.ConfirmAsync(Resources.Confirm_DiscardEdit, Resources.Common_Discard))
        {
            return;
        }
        Close(false);
    }

    /// <summary>
    /// Formats a Unix-seconds timestamp in local time with the language's pattern.
    /// </summary>
    /// <param name="unixSeconds">The timestamp.</param>
    /// <returns>The formatted time.</returns>
    private static string FormatTime(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime()
            .ToString(Resources.Common_TimePattern, Resources.Culture);
}
