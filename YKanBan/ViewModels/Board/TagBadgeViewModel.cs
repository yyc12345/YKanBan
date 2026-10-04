using Avalonia.Media;
using Avalonia.Media.Immutable;
using YKanBan.Models;

namespace YKanBan.ViewModels.Board;

/// <summary>
/// A colored tag badge (color + name). Tag colors are user data: they are shown
/// as the literal #RRGGBB value in both themes, with a black or white text color
/// chosen for contrast.
/// </summary>
public sealed class TagBadgeViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes the badge.
    /// </summary>
    /// <param name="tag">The tag shown.</param>
    public TagBadgeViewModel(TagModel tag)
    {
        Tag = tag;
        Background = new ImmutableSolidColorBrush(Color.FromRgb(tag.Color.R, tag.Color.G, tag.Color.B));
        Foreground = IsLight(tag.Color) ? Brushes.Black : Brushes.White;
    }

    /// <summary>
    /// Gets the tag shown.
    /// </summary>
    public TagModel Tag { get; }

    /// <summary>
    /// Gets the tag name.
    /// </summary>
    public string Name => Tag.Name;

    /// <summary>
    /// Gets the tag description.
    /// </summary>
    public string Description => Tag.Description;

    /// <summary>
    /// Gets the badge tooltip: the description, or <see langword="null"/> (no
    /// tooltip at all) when the description is empty.
    /// </summary>
    public string? ToolTip => Tag.Description.Length > 0 ? Tag.Description : null;

    /// <summary>
    /// Gets the badge fill: the tag's own color.
    /// </summary>
    public IBrush Background { get; }

    /// <summary>
    /// Gets the text color contrasting with <see cref="Background"/>.
    /// </summary>
    public IBrush Foreground { get; }

    /// <summary>
    /// Decides whether dark text reads better on a color, by its relative luminance.
    /// </summary>
    /// <param name="color">The background color.</param>
    /// <returns><see langword="true"/> for light colors.</returns>
    public static bool IsLight(RgbColor color) =>
        (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B) > 150;
}
