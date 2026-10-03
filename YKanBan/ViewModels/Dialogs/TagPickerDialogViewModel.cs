using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Models;
using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.ViewModels.Board;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The nested "add tag" modal of the card editor: a filterable list of the
/// existing tags not yet on the card, plus a new-tag entry. Picking a tag closes
/// the dialog with it as <see cref="PickedTag"/>.
///
/// Exception to the edit-dialog unit of work: a tag created here is written to
/// the tags table immediately (tags are global entities), so it survives even
/// when the outer card edit is cancelled — only the assignment is discarded.
/// </summary>
public sealed partial class TagPickerDialogViewModel : DialogViewModelBase
{
    /// <summary>
    /// Default colors proposed for new tags, cycled by the number of existing tags.
    /// </summary>
    private static readonly RgbColor[] Palette =
    [
        new(0xd7, 0x3a, 0x4a), new(0x00, 0x75, 0xca), new(0x0e, 0x8a, 0x16), new(0xfb, 0xca, 0x04),
        new(0x7c, 0x3a, 0xed), new(0xe9, 0x9b, 0x3c), new(0x00, 0x8b, 0x8b), new(0xcf, 0xd3, 0xd7),
    ];

    private readonly WorkspaceRepository _repository;
    private readonly IReadOnlyList<TagBadgeViewModel> _available;

    /// <summary>
    /// Initializes the picker.
    /// </summary>
    /// <param name="repository">The workspace repository; new tags are written through it immediately.</param>
    /// <param name="assignedTagIds">The tags already on the card, which are not offered again.</param>
    public TagPickerDialogViewModel(WorkspaceRepository repository, IReadOnlyCollection<long> assignedTagIds)
    {
        _repository = repository;
        IReadOnlyList<TagModel> all = repository.GetTags();
        _available = all
            .Where(tag => !assignedTagIds.Contains(tag.Id))
            .Select(tag => new TagBadgeViewModel(tag))
            .ToList();
        FilteredTags = new ObservableCollection<TagBadgeViewModel>(_available);

        newColor = Palette[all.Count % Palette.Length].ToString();
        UpdateColorPreview();
    }

    /// <summary>
    /// Gets the tag the user picked or created, once the dialog closed positively.
    /// </summary>
    public TagModel? PickedTag { get; private set; }

    /// <summary>
    /// Gets the available tags matching <see cref="FilterText"/>.
    /// </summary>
    public ObservableCollection<TagBadgeViewModel> FilteredTags { get; }

    /// <summary>
    /// Gets whether no available tag matches the filter.
    /// </summary>
    public bool NoMatches => FilteredTags.Count == 0;

    /// <summary>
    /// Gets or sets the list filter: a case-insensitive substring of the tag name.
    /// </summary>
    [ObservableProperty]
    private string filterText = "";

    /// <summary>
    /// Gets or sets the name of the tag to create (not trimmed).
    /// </summary>
    [ObservableProperty]
    private string newName = "";

    /// <summary>
    /// Gets or sets the #RRGGBB color text of the tag to create.
    /// </summary>
    [ObservableProperty]
    private string newColor;

    /// <summary>
    /// Gets or sets the description of the tag to create.
    /// </summary>
    [ObservableProperty]
    private string newDescription = "";

    /// <summary>
    /// Gets the preview of <see cref="NewColor"/>, transparent while it is not valid.
    /// </summary>
    [ObservableProperty]
    private IBrush newColorPreview = Brushes.Transparent;

    /// <summary>
    /// Gets the localized error of the last create attempt, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    /// <summary>
    /// Gets whether an error is shown.
    /// </summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>
    /// Picks an existing tag and closes.
    /// </summary>
    /// <param name="tag">The picked tag.</param>
    [RelayCommand]
    private void Pick(TagBadgeViewModel tag)
    {
        PickedTag = tag.Tag;
        Close(true);
    }

    /// <summary>
    /// Creates the new tag right away and closes with it picked; constraint
    /// violations and an invalid color keep the dialog open with an error.
    /// </summary>
    [RelayCommand]
    private void Create()
    {
        if (!RgbColor.TryParse(NewColor, out RgbColor color))
        {
            ErrorMessage = Resources.Error_Tag_InvalidColor;
            return;
        }

        try
        {
            PickedTag = _repository.AddTag(NewName, color, NewDescription);
        }
        catch (Exception exception)
        {
            ErrorMessage = WriteErrorMessages.Describe(exception);
            return;
        }
        Close(true);
    }

    /// <summary>
    /// Closes without picking anything.
    /// </summary>
    [RelayCommand]
    private Task CancelAsync() => RequestCancelAsync();

    /// <summary>
    /// Re-filters the list.
    /// </summary>
    /// <param name="value">The new filter text.</param>
    partial void OnFilterTextChanged(string value)
    {
        string folded = SqliteDatabase.Fold(value);
        FilteredTags.Clear();
        foreach (TagBadgeViewModel tag in _available)
        {
            if (SqliteDatabase.Fold(tag.Name).Contains(folded, StringComparison.Ordinal))
            {
                FilteredTags.Add(tag);
            }
        }
        OnPropertyChanged(nameof(NoMatches));
    }

    /// <summary>
    /// Refreshes the color preview.
    /// </summary>
    /// <param name="value">The new color text.</param>
    partial void OnNewColorChanged(string value) => UpdateColorPreview();

    /// <summary>
    /// Recomputes <see cref="NewColorPreview"/> from <see cref="NewColor"/>.
    /// </summary>
    private void UpdateColorPreview() =>
        NewColorPreview = RgbColor.TryParse(NewColor, out RgbColor color)
            ? new ImmutableSolidColorBrush(Color.FromRgb(color.R, color.G, color.B))
            : Brushes.Transparent;
}
