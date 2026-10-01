using YKanBan.Search;

namespace YKanBan.Tests.Search;

/// <summary>
/// Structural comparer for search AST nodes, used by assertions only. The
/// AST records compare by reference for their Terms collections by design
/// (see the YYC MARK note in SearchNodes.cs), so tests provide equality
/// explicitly.
/// </summary>
public sealed class SearchNodeComparer : IEqualityComparer<SearchNode>
{
    /// <summary>Shared comparer instance.</summary>
    public static SearchNodeComparer Instance { get; } = new();

    /// <summary>Determines structural equality of two nodes.</summary>
    public bool Equals(SearchNode? x, SearchNode? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }
        if (x is null || y is null || x.GetType() != y.GetType())
        {
            return false;
        }

        return (x, y) switch
        {
            (SearchNode.Or a, SearchNode.Or b) => SequencesEqual(a.Terms, b.Terms),
            (SearchNode.And a, SearchNode.And b) => SequencesEqual(a.Terms, b.Terms),
            (SearchNode.FreeText a, SearchNode.FreeText b) => a.Text == b.Text,
            (SearchNode.FieldText a, SearchNode.FieldText b) => a.Field == b.Field && a.Text == b.Text,
            (SearchNode.TagName a, SearchNode.TagName b) => a.Name == b.Name,
            (SearchNode.ColumnTitle a, SearchNode.ColumnTitle b) => a.Title == b.Title,
            (SearchNode.CardId a, SearchNode.CardId b) => a.Id == b.Id,
            _ => false,
        };
    }

    /// <summary>Not supported: the comparer exists for assertions, not hashing.</summary>
    public int GetHashCode(SearchNode obj) =>
        throw new NotSupportedException("Structural comparison for assertions only; hashing is not supported.");

    /// <summary>Order-sensitive element-wise comparison.</summary>
    private static bool SequencesEqual(IReadOnlyList<SearchNode> x, IReadOnlyList<SearchNode> y)
    {
        if (x.Count != y.Count)
        {
            return false;
        }
        return Enumerable.Zip(x, y, (a, b) => (a, b)).All(pair => Instance.Equals(pair.a, pair.b));
    }
}
