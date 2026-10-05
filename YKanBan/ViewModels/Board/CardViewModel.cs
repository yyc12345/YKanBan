using CommunityToolkit.Mvvm.Input;

using YKanBan.Models;

namespace YKanBan.ViewModels.Board;

/// <summary>
/// One card on the board. Clicking the card body opens the editor; the corner
/// menu offers delete and move. The shown text is the "#id title" heading, a
/// truncated content preview and the colored tag badges; timestamps are not
/// shown on the board.
/// </summary>
public sealed partial class CardViewModel : ViewModelBase {
    /// <summary>
    /// Maximum number of content characters shown in the card preview.
    /// </summary>
    private const int PreviewLength = 200;

    /// <summary>
    /// Maximum number of explicit content lines shown in the card preview.
    /// </summary>
    private const int PreviewMaxLines = 10;

    private readonly BoardViewModel _board;

    /// <summary>
    /// Initializes the card view from its model.
    /// </summary>
    /// <param name="board">The owning board view model.</param>
    /// <param name="card">The card model to display.</param>
    public CardViewModel(BoardViewModel board, CardModel card) {
        _board = board;
        Model = card;
        TagBadges = card.Tags.Select(tag => new TagBadgeViewModel(tag)).ToList();
    }

    /// <summary>
    /// Gets the underlying card model.
    /// </summary>
    public CardModel Model { get; }

    /// <summary>
    /// Gets the card id.
    /// </summary>
    public long Id => Model.Id;

    /// <summary>
    /// Gets the owning column id.
    /// </summary>
    public long ColumnId => Model.ColumnId;

    /// <summary>
    /// Gets the tag badges shown under the content.
    /// </summary>
    public IReadOnlyList<TagBadgeViewModel> TagBadges { get; }

    /// <summary>
    /// Gets the bold id marker, for example "#61".
    /// </summary>
    public string IdText => $"#{Model.Id}";

    /// <summary>
    /// Gets the card title, empty when the card has no title.
    /// </summary>
    public string TitleText => Model.Title;

    /// <summary>
    /// Gets a value indicating whether the card has a title to show.
    /// </summary>
    public bool HasTitle => Model.Title.Length > 0;

    /// <summary>
    /// Gets a value indicating whether the card has content to preview.
    /// </summary>
    public bool HasContent => Model.Content.Length > 0;

    /// <summary>
    /// Gets the content preview, truncated to at most 200 characters and
    /// 10 explicit lines, with an ellipsis when anything was cut.
    /// </summary>
    // YYC MARK: the preview is truncated here instead of in the view because
    // both a character cap and an explicit-line cap are needed, and because
    // Avalonia 11.3.x renders no ellipsis when MaxLines clips wrapped text
    // (fixed upstream in 12.1.2, PR #22017, never backported). Revisit after
    // upgrading Avalonia.
    public string PreviewContent {
        get {
            string content = Model.Content;

            // Cut at whichever boundary comes first: 200 characters or the 10th line break.
            int limit = content.Length;
            int breaks = 0;
            for (int i = 0; i < content.Length; i++) {
                if (i == PreviewLength || (content[i] == '\n' && ++breaks == PreviewMaxLines)) {
                    limit = i;
                    break;
                }
            }

            return limit < content.Length ? content[..limit] + "…" : content;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the card has any tags.
    /// </summary>
    public bool HasTags => TagBadges.Count > 0;

    /// <summary>
    /// Opens the card editor.
    /// </summary>
    [RelayCommand]
    private void Edit() => _ = _board.RequestEditCardAsync(this);

    /// <summary>
    /// Asks the board to delete this card.
    /// </summary>
    [RelayCommand]
    private void Delete() => _ = _board.RequestDeleteCardAsync(this);

    /// <summary>
    /// Asks the board to move this card to another column.
    /// </summary>
    [RelayCommand]
    private void Move() => _ = _board.RequestMoveCardAsync(this);
}
