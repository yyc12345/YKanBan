namespace YKanBan.Models;

/// <summary>
/// A card with its assigned tags loaded.
/// </summary>
/// <param name="Id">Row id.</param>
/// <param name="ColumnId">Owning column.</param>
/// <param name="Title">Possibly empty; the empty string is the canonical "no title".</param>
/// <param name="Content">Multiline body, possibly empty.</param>
/// <param name="CreatedAt">Unix seconds of creation.</param>
/// <param name="UpdatedAt">Unix seconds of the last confirmed edit write.</param>
/// <param name="Tags">Tags assigned to this card, ordered by tag id.</param>
public sealed record CardModel(
    long Id,
    long ColumnId,
    string Title,
    string Content,
    long CreatedAt,
    long UpdatedAt,
    IReadOnlyList<TagModel> Tags);
