using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Models;

namespace YKanBan.ViewModels.Board;

/// <summary>
/// One card in a column list: "#id" in bold on the same line as the title, the
/// content cut to a fixed length, and the tag badges below. No timestamps.
/// Clicking the card opens the editor; its menu deletes or moves it.
/// </summary>
public sealed partial class CardViewModel : ViewModelBase
{
    /// <summary>
    /// The number of text elements of content shown on the card.
    /// </summary>
    public const int ExcerptLength = 200;

    private readonly BoardViewModel _board;

    /// <summary>
    /// Initializes the card.
    /// </summary>
    /// <param name="board">The owning board.</param>
    /// <param name="card">The card shown.</param>
    public CardViewModel(BoardViewModel board, CardModel card)
    {
        _board = board;
        Card = card;
        IdText = $"#{card.Id.ToString(CultureInfo.InvariantCulture)}";
        Excerpt = MakeExcerpt(card.Content);
        Tags = card.Tags.Select(tag => new TagBadgeViewModel(tag)).ToList();
    }

    /// <summary>
    /// Gets the card shown.
    /// </summary>
    public CardModel Card { get; }

    /// <summary>
    /// Gets the "#id" text.
    /// </summary>
    public string IdText { get; }

    /// <summary>
    /// Gets the title; empty when the card has none.
    /// </summary>
    public string Title => Card.Title;

    /// <summary>
    /// Gets the title as it follows "#id" on the header line: a space and the
    /// title, or nothing when the card has no title.
    /// </summary>
    public string TitleSuffix => Card.Title.Length == 0 ? "" : " " + Card.Title;

    /// <summary>
    /// Gets the truncated content.
    /// </summary>
    public string Excerpt { get; }

    /// <summary>
    /// Gets whether there is content to show.
    /// </summary>
    public bool HasExcerpt => Excerpt.Length > 0;

    /// <summary>
    /// Gets the tag badges.
    /// </summary>
    public IReadOnlyList<TagBadgeViewModel> Tags { get; }

    /// <summary>
    /// Gets whether the card has tags.
    /// </summary>
    public bool HasTags => Tags.Count > 0;

    /// <summary>
    /// Gets the "move to column" targets; built when the menu asks for them.
    /// The card's current column is listed but disabled.
    /// </summary>
    public IReadOnlyList<MoveTargetViewModel> MoveTargets =>
        _board.ColumnModels
            .Select(column => new MoveTargetViewModel(column.Title, column.Id != Card.ColumnId, MoveToCommand, column.Id))
            .ToList();

    /// <summary>
    /// Formats the "#id title" header text of a card ("#id" alone without a title).
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The header text.</returns>
    public static string FormatHeader(CardModel card) =>
        card.Title.Length == 0
            ? $"#{card.Id.ToString(CultureInfo.InvariantCulture)}"
            : $"#{card.Id.ToString(CultureInfo.InvariantCulture)} {card.Title}";

    /// <summary>
    /// Cuts content to <see cref="ExcerptLength"/> text elements, appending an
    /// ellipsis when something was cut. Surrogate pairs and combining sequences
    /// are never split.
    /// </summary>
    /// <param name="content">The card content.</param>
    /// <returns>The excerpt.</returns>
    public static string MakeExcerpt(string content)
    {
        string trimmed = content.Trim();
        var info = new StringInfo(trimmed);
        return info.LengthInTextElements <= ExcerptLength
            ? trimmed
            : info.SubstringByTextElements(0, ExcerptLength).TrimEnd() + "…";
    }

    /// <summary>
    /// Opens the card editor.
    /// </summary>
    /// <returns>A task completing when the editor closes.</returns>
    [RelayCommand]
    private Task EditAsync() => _board.EditCardAsync(Card.Id);

    /// <summary>
    /// Deletes the card.
    /// </summary>
    /// <returns>A task completing when the card is deleted or the deletion was declined.</returns>
    [RelayCommand]
    private Task DeleteAsync() => _board.DeleteCardAsync(Card);

    /// <summary>
    /// Moves the card to another column.
    /// </summary>
    /// <param name="columnId">The target column.</param>
    /// <returns>A task completing when the card is moved.</returns>
    [RelayCommand]
    private Task MoveToAsync(long columnId) => _board.MoveCardAsync(Card.Id, columnId);
}

/// <summary>
/// An entry of a card's "move to column" submenu.
/// </summary>
/// <param name="Title">The column title.</param>
/// <param name="IsEnabled">Whether the entry can be picked (false for the card's own column).</param>
/// <param name="Command">The move command.</param>
/// <param name="ColumnId">The target column, passed to <paramref name="Command"/>.</param>
public sealed record MoveTargetViewModel(string Title, bool IsEnabled, IAsyncRelayCommand<long> Command, long ColumnId);
