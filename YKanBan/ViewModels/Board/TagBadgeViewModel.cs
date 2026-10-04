using Avalonia.Media;

using YKanBan.Models;

namespace YKanBan.ViewModels.Board;

/// <summary>
/// A read-only colored tag badge shown on a card: the tag name over a
/// background taken directly from the tag color (user data, never themed) and
/// an automatically contrasting text color.
/// </summary>
public sealed class TagBadgeViewModel {
    /// <summary>
    /// Initializes a badge from a tag.
    /// </summary>
    /// <param name="tag">The tag to render.</param>
    public TagBadgeViewModel(TagModel tag) {
        Tag = tag;
        Name = tag.Name;
        Color background = Color.FromRgb(tag.Color.R, tag.Color.G, tag.Color.B);
        Background = new SolidColorBrush(background);
        Foreground = new SolidColorBrush(ContrastingTextColor(background));
    }

    /// <summary>
    /// Gets the underlying tag model.
    /// </summary>
    public TagModel Tag { get; }

    /// <summary>
    /// Gets the tag name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the tooltip text: the tag description when present, otherwise
    /// <see langword="null"/> so no tooltip is shown.
    /// </summary>
    public string? ToolTipText => Tag.Description.Length > 0 ? Tag.Description : null;

    /// <summary>
    /// Gets the badge background brush.
    /// </summary>
    public IBrush Background { get; }

    /// <summary>
    /// Gets the badge text brush.
    /// </summary>
    public IBrush Foreground { get; }

    /// <summary>
    /// Picks black or white text for readability against the given background.
    /// </summary>
    /// <param name="background">The badge background color.</param>
    /// <returns>The contrasting text color.</returns>
    private static Color ContrastingTextColor(Color background) {
        double luminance = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;
        return luminance > 0.6 ? Colors.Black : Colors.White;
    }
}
