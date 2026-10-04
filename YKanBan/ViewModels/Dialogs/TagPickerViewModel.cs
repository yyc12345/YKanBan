using CommunityToolkit.Mvvm.ComponentModel;

using YKanBan.Storage.Workspace;
using YKanBan.ViewModels.Board;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The nested tag picker opened from the card editor: choose one tag that is
/// not already assigned to the card. When no candidate remains the picker
/// shows a message and cannot be confirmed.
/// </summary>
public sealed partial class TagPickerViewModel : ViewModelBase {
    /// <summary>
    /// Initializes the picker with the tags not yet assigned to the card.
    /// </summary>
    /// <param name="repository">The workspace repository.</param>
    /// <param name="assignedTagIds">The tag ids already assigned to the card.</param>
    public TagPickerViewModel(WorkspaceRepository repository, IReadOnlyCollection<long> assignedTagIds) {
        Targets = repository.GetTags()
            .Where(tag => !assignedTagIds.Contains(tag.Id))
            .Select(tag => new TagBadgeViewModel(tag))
            .ToList();
        selectedTarget = Targets.FirstOrDefault();
    }

    /// <summary>
    /// Gets the assignable tags.
    /// </summary>
    public IReadOnlyList<TagBadgeViewModel> Targets { get; }

    /// <summary>
    /// Gets or sets the selected tag.
    /// </summary>
    [ObservableProperty]
    private TagBadgeViewModel? selectedTarget;

    /// <summary>
    /// Gets a value indicating whether there is a tag to add.
    /// </summary>
    public bool CanAdd => Targets.Count > 0;
}
