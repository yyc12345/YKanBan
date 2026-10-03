using YKanBan.Models;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The default colors proposed for new tags, shared by the tag picker and the
/// tag editor.
/// </summary>
public static class TagColors
{
    /// <summary>
    /// Gets the palette, offered as swatches and cycled for new-tag defaults.
    /// </summary>
    public static IReadOnlyList<RgbColor> Palette { get; } =
    [
        new(0xd7, 0x3a, 0x4a), new(0x00, 0x75, 0xca), new(0x0e, 0x8a, 0x16), new(0xfb, 0xca, 0x04),
        new(0x7c, 0x3a, 0xed), new(0xe9, 0x9b, 0x3c), new(0x00, 0x8b, 0x8b), new(0xcf, 0xd3, 0xd7),
    ];

    /// <summary>
    /// Returns the default color of a new tag, cycled by the number of existing tags.
    /// </summary>
    /// <param name="existingCount">The number of tags already in the workspace.</param>
    /// <returns>The proposed color.</returns>
    public static RgbColor DefaultFor(int existingCount) => Palette[existingCount % Palette.Count];
}
