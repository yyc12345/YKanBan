using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Models;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The new-tag / edit-tag dialog of the tags tab: name (required, not trimmed),
/// #RRGGBB color with palette swatches and a live preview, and an optional
/// description. Confirming writes through the supplied save callback; an invalid
/// color or a schema-constraint violation (empty or duplicate name) is shown
/// inside the dialog, which stays open with the user's input intact.
/// </summary>
public sealed partial class TagEditorDialogViewModel : DialogViewModelBase
{
    private readonly Action<string, RgbColor, string> _save;

    /// <summary>
    /// Initializes the dialog.
    /// </summary>
    /// <param name="existing">The tag being edited, or <see langword="null"/> for a new tag.</param>
    /// <param name="defaultColor">The color proposed for a new tag.</param>
    /// <param name="save">Writes the name, color and description; throws the repository's typed exceptions.</param>
    public TagEditorDialogViewModel(TagModel? existing, RgbColor defaultColor, Action<string, RgbColor, string> save)
    {
        _save = save;
        Title = existing is null ? Resources.TagEditor_TitleNew : Resources.TagEditor_TitleEdit;
        name = existing?.Name ?? "";
        color = (existing?.Color ?? defaultColor).ToString();
        description = existing?.Description ?? "";
        Swatches = TagColors.Palette.Select(swatch => new ColorSwatch(swatch.ToString(), ToBrush(swatch))).ToList();
        UpdateColorPreview();
    }

    /// <summary>
    /// Gets the localized window title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the palette swatches; clicking one sets <see cref="Color"/>.
    /// </summary>
    public IReadOnlyList<ColorSwatch> Swatches { get; }

    /// <summary>
    /// Gets or sets the tag name being edited (not trimmed).
    /// </summary>
    [ObservableProperty]
    private string name;

    /// <summary>
    /// Gets or sets the #RRGGBB color text being edited.
    /// </summary>
    [ObservableProperty]
    private string color;

    /// <summary>
    /// Gets or sets the tag description being edited.
    /// </summary>
    [ObservableProperty]
    private string description;

    /// <summary>
    /// Gets the preview of <see cref="Color"/>, transparent while it is not valid.
    /// </summary>
    [ObservableProperty]
    private IBrush colorPreview = Brushes.Transparent;

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
    /// Takes a swatch color.
    /// </summary>
    /// <param name="hex">The swatch's #RRGGBB text.</param>
    [RelayCommand]
    private void PickSwatch(string hex) => Color = hex;

    /// <summary>
    /// Writes the tag; closes on success, otherwise shows the error.
    /// </summary>
    [RelayCommand]
    private void Confirm()
    {
        if (!RgbColor.TryParse(Color, out RgbColor parsed))
        {
            ErrorMessage = Resources.Error_Tag_InvalidColor;
            return;
        }

        try
        {
            _save(Name, parsed, Description);
        }
        catch (Exception exception)
        {
            ErrorMessage = WriteErrorMessages.Describe(exception);
            return;
        }
        Close(true);
    }

    /// <summary>
    /// Closes without writing.
    /// </summary>
    [RelayCommand]
    private Task CancelAsync() => RequestCancelAsync();

    /// <summary>
    /// Refreshes the color preview.
    /// </summary>
    /// <param name="value">The new color text.</param>
    partial void OnColorChanged(string value) => UpdateColorPreview();

    /// <summary>
    /// Recomputes <see cref="ColorPreview"/> from <see cref="Color"/>.
    /// </summary>
    private void UpdateColorPreview() =>
        ColorPreview = RgbColor.TryParse(Color, out RgbColor parsed) ? ToBrush(parsed) : Brushes.Transparent;

    /// <summary>
    /// Creates the thread-agnostic brush of a tag color.
    /// </summary>
    /// <param name="value">The color.</param>
    /// <returns>The brush.</returns>
    private static IBrush ToBrush(RgbColor value) =>
        new ImmutableSolidColorBrush(Avalonia.Media.Color.FromRgb(value.R, value.G, value.B));
}

/// <summary>
/// A palette swatch of the tag editor.
/// </summary>
/// <param name="Hex">The #RRGGBB text.</param>
/// <param name="Brush">The fill.</param>
public sealed record ColorSwatch(string Hex, IBrush Brush);
