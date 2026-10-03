using System.Text;
using YKanBan.Storage.AppData;

namespace YKanBan.Models;

/// <summary>
/// Computes the in-column card display order for a <see cref="CardSortOption"/>.
/// Columns are not affected: they always stay in id order. Every option falls
/// back to id ascending as the final tie-breaker, so the order is total.
/// </summary>
public static class CardOrdering
{
    /// <summary>
    /// Orders cards for display.
    /// </summary>
    /// <param name="cards">The cards to order, typically those of one column.</param>
    /// <param name="option">The selected sort option.</param>
    /// <returns>The cards in display order.</returns>
    public static IReadOnlyList<CardModel> Sort(IEnumerable<CardModel> cards, CardSortOption option)
    {
        IOrderedEnumerable<CardModel> ordered = option switch
        {
            CardSortOption.Id => cards.OrderBy(card => card.Id),

            // Most recently modified first.
            CardSortOption.UpdatedAt => cards.OrderByDescending(card => card.UpdatedAt).ThenBy(card => card.Id),

            // Code point order, matching SQLite's BINARY collation; the empty title sorts first.
            CardSortOption.Title => cards.OrderBy(card => card.Title, CodePointComparer.Instance).ThenBy(card => card.Id),

            _ => throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown card sort option."),
        };
        return ordered.ToList();
    }

    /// <summary>
    /// Compares strings by Unicode code points. Unlike ordinal UTF-16 comparison
    /// it orders supplementary characters after U+E000–U+FFFF, as BINARY does on UTF-8.
    /// </summary>
    private sealed class CodePointComparer : IComparer<string>
    {
        public static readonly CodePointComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x is null || y is null)
            {
                return x is null ? (y is null ? 0 : -1) : 1;
            }

            StringRuneEnumerator left = x.EnumerateRunes();
            StringRuneEnumerator right = y.EnumerateRunes();
            while (true)
            {
                bool hasLeft = left.MoveNext();
                bool hasRight = right.MoveNext();
                if (!hasLeft || !hasRight)
                {
                    return hasLeft.CompareTo(hasRight);
                }

                int result = left.Current.Value.CompareTo(right.Current.Value);
                if (result != 0)
                {
                    return result;
                }
            }
        }
    }
}
