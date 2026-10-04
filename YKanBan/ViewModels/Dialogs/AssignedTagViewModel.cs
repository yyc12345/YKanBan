using Avalonia.Media;

using CommunityToolkit.Mvvm.Input;

using YKanBan.Models;
using YKanBan.ViewModels.Board;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// One tag assigned to the card being edited, shown with a colored badge and a
/// remove action that only changes the in-memory assignment (the actual write
/// happens on confirm).
/// </summary>
public sealed partial class AssignedTagViewModel {
    private readonly CardEditorViewModel _editor;
    private readonly TagBadgeViewModel _badge;

    /// <summary>
    /// Initializes the assigned-tag row.
    /// </summary>
    /// <param name="editor">The owning card editor.</param>
    /// <param name="tag">The assigned tag.</param>
    public AssignedTagViewModel(CardEditorViewModel editor, TagModel tag) {
        _editor = editor;
        Tag = tag;
        _badge = new TagBadgeViewModel(tag);
    }

    /// <summary>
    /// Gets the assigned tag model.
    /// </summary>
    public TagModel Tag { get; }

    /// <summary>
    /// Gets the tag name.
    /// </summary>
    public string Name => _badge.Name;

    /// <summary>
    /// Gets the badge background brush.
    /// </summary>
    public IBrush Background => _badge.Background;

    /// <summary>
    /// Gets the badge text brush.
    /// </summary>
    public IBrush Foreground => _badge.Foreground;

    /// <summary>
    /// Removes this tag from the in-memory assignment.
    /// </summary>
    [RelayCommand]
    private void Remove() => _editor.RemoveTag(Tag);
}
