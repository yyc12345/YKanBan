using CommunityToolkit.Mvvm.Input;
using YKanBan.Models;
using YKanBan.ViewModels.Board;

namespace YKanBan.ViewModels.Tags;

/// <summary>
/// One row of the tags tab: the colored badge, the description and the usage
/// count, with edit and delete actions.
/// </summary>
public sealed partial class TagRowViewModel : ViewModelBase
{
    private readonly TagsViewModel _owner;

    /// <summary>
    /// Initializes the row.
    /// </summary>
    /// <param name="owner">The tags tab performing the actions.</param>
    /// <param name="tag">The tag shown.</param>
    /// <param name="usageCount">The number of cards carrying the tag.</param>
    public TagRowViewModel(TagsViewModel owner, TagModel tag, long usageCount)
    {
        _owner = owner;
        Tag = tag;
        Badge = new TagBadgeViewModel(tag);
        UsageCount = usageCount;
    }

    /// <summary>
    /// Gets the tag shown.
    /// </summary>
    public TagModel Tag { get; }

    /// <summary>
    /// Gets the colored badge (color + name).
    /// </summary>
    public TagBadgeViewModel Badge { get; }

    /// <summary>
    /// Gets the tag description.
    /// </summary>
    public string Description => Tag.Description;

    /// <summary>
    /// Gets whether the tag has a description.
    /// </summary>
    public bool HasDescription => Tag.Description.Length > 0;

    /// <summary>
    /// Gets the #RRGGBB color text.
    /// </summary>
    public string ColorText => Tag.Color.ToString();

    /// <summary>
    /// Gets the number of cards carrying the tag.
    /// </summary>
    public long UsageCount { get; }

    /// <summary>
    /// Gets the localized usage count, for example "3 cards".
    /// </summary>
    public string UsageText => string.Format(Resources.Culture, Resources.Tags_UsageCount, UsageCount);

    /// <summary>
    /// Opens the edit dialog.
    /// </summary>
    /// <returns>A task completing when the dialog closes.</returns>
    [RelayCommand]
    private Task EditAsync() => _owner.EditTagAsync(Tag);

    /// <summary>
    /// Deletes the tag.
    /// </summary>
    /// <returns>A task completing when the tag is deleted or the deletion was declined.</returns>
    [RelayCommand]
    private Task DeleteAsync() => _owner.DeleteTagAsync(Tag, UsageCount);
}
