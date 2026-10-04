using System.Globalization;

using CommunityToolkit.Mvvm.Input;

using YKanBan.Models;
using YKanBan.ViewModels.Board;

namespace YKanBan.ViewModels.Tags;

/// <summary>
/// One row of the tag manager: a color swatch, the name, the description and the
/// usage count, plus edit and delete actions.
/// </summary>
public sealed partial class TagItemViewModel {
    private readonly TagsViewModel _owner;

    /// <summary>
    /// Initializes the row.
    /// </summary>
    /// <param name="owner">The owning tag manager.</param>
    /// <param name="tag">The tag model.</param>
    /// <param name="usageCount">The number of cards using the tag.</param>
    public TagItemViewModel(TagsViewModel owner, TagModel tag, long usageCount) {
        _owner = owner;
        Tag = tag;
        Badge = new TagBadgeViewModel(tag);
        UsageCount = usageCount;
        UsageText = string.Format(
            Resources.Culture ?? CultureInfo.CurrentCulture, Resources.Tags_Usage, usageCount);
    }

    /// <summary>
    /// Gets the underlying tag model.
    /// </summary>
    public TagModel Tag { get; }

    /// <summary>
    /// Gets the colored badge for the swatch and name.
    /// </summary>
    public TagBadgeViewModel Badge { get; }

    /// <summary>
    /// Gets the tag name.
    /// </summary>
    public string Name => Tag.Name;

    /// <summary>
    /// Gets the tag description.
    /// </summary>
    public string Description => Tag.Description;

    /// <summary>
    /// Gets a value indicating whether the tag has a description.
    /// </summary>
    public bool HasDescription => Tag.Description.Length > 0;

    /// <summary>
    /// Gets the number of cards using the tag.
    /// </summary>
    public long UsageCount { get; }

    /// <summary>
    /// Gets the localized usage count.
    /// </summary>
    public string UsageText { get; }

    /// <summary>
    /// Asks the manager to edit this tag.
    /// </summary>
    [RelayCommand]
    private void Edit() => _ = _owner.RequestEditAsync(this);

    /// <summary>
    /// Asks the manager to delete this tag.
    /// </summary>
    [RelayCommand]
    private void Delete() => _ = _owner.RequestDeleteAsync(this);
}
