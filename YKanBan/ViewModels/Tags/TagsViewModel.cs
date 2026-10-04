using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.Input;

using YKanBan.Models;
using YKanBan.Storage.Workspace;

namespace YKanBan.ViewModels.Tags;

/// <summary>
/// The tags tab: a manager for the workspace's tags (create, rename, recolor,
/// re-describe and delete), showing each tag's color, name, description and
/// usage count. Tag changes also refresh the board.
/// </summary>
public sealed partial class TagsViewModel : ViewModelBase {
    private readonly WorkspaceRepository _repository;
    private readonly Action _boardChanged;

    /// <summary>
    /// Initializes the manager, loading the current tags.
    /// </summary>
    /// <param name="repository">The workspace repository.</param>
    /// <param name="boardChanged">Invoked after a tag change so the board refreshes.</param>
    public TagsViewModel(WorkspaceRepository repository, Action boardChanged) {
        _repository = repository;
        _boardChanged = boardChanged;
        Reload();
    }

    /// <summary>
    /// Gets the workspace repository used for tag access.
    /// </summary>
    public WorkspaceRepository Repository => _repository;

    /// <summary>
    /// Gets the tags in id order.
    /// </summary>
    public ObservableCollection<TagItemViewModel> Tags { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the workspace has any tags.
    /// </summary>
    public bool HasTags => Tags.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the workspace has no tags.
    /// </summary>
    public bool HasNoTags => Tags.Count == 0;

    /// <summary>
    /// Gets the interaction that opens the new-tag editor.
    /// </summary>
    public Interaction<Unit> AddTag { get; } = new();

    /// <summary>
    /// Gets the interaction that opens the tag editor.
    /// </summary>
    public Interaction<TagItemViewModel> EditTag { get; } = new();

    /// <summary>
    /// Gets the interaction that confirms and deletes a tag.
    /// </summary>
    public Interaction<TagItemViewModel> DeleteTag { get; } = new();

    /// <summary>
    /// Opens the new-tag editor.
    /// </summary>
    [RelayCommand]
    private Task AddNewTagAsync() => AddTag.Handle(Unit.Value);

    /// <summary>
    /// Requests the editor for a tag.
    /// </summary>
    /// <param name="item">The tag to edit.</param>
    /// <returns>A task completing after the editor closes.</returns>
    public Task RequestEditAsync(TagItemViewModel item) => EditTag.Handle(item);

    /// <summary>
    /// Requests deletion of a tag.
    /// </summary>
    /// <param name="item">The tag to delete.</param>
    /// <returns>A task completing after confirmation.</returns>
    public Task RequestDeleteAsync(TagItemViewModel item) => DeleteTag.Handle(item);

    /// <summary>
    /// Reloads the tag list from the repository.
    /// </summary>
    public void Reload() {
        Tags.Clear();
        foreach ((TagModel tag, long usage) in _repository.GetTagsWithUsage()) {
            Tags.Add(new TagItemViewModel(this, tag, usage));
        }

        OnPropertyChanged(nameof(HasTags));
        OnPropertyChanged(nameof(HasNoTags));
    }

    /// <summary>
    /// Reloads the tags and refreshes the board after a tag change.
    /// </summary>
    public void RefreshAfterChange() {
        Reload();
        _boardChanged();
    }
}
