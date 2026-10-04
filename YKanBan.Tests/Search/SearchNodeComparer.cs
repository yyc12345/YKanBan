using YKanBan.Search;

namespace YKanBan.Tests.Search;

/// <summary>
/// Structural comparer for search AST nodes, used by assertions only. The AST
/// records compare by reference for their Terms collections by design (see the
/// YYC MARK note in SearchNodes.cs), so tests provide equality explicitly.
/// </summary>
public sealed class SearchNodeComparer : IEqualityComparer<SearchNode> {
    /// <summary>
    /// Gets the shared comparer instance.
    /// </summary>
    public static SearchNodeComparer Instance { get; } = new();

    /// <summary>
    /// Determines structural equality of two nodes.
    /// </summary>
    /// <param name="x">The first node.</param>
    /// <param name="y">The second node.</param>
    /// <returns><see langword="true"/> when the nodes are structurally equal.</returns>
    public bool Equals(SearchNode? x, SearchNode? y) {
        if (ReferenceEquals(x, y)) {
            return true;
        }
        if (x is null || y is null || x.GetType() != y.GetType()) {
            return false;
        }

        return (x, y) switch {
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

    /// <summary>
    /// Not supported: the comparer exists for assertions, not hashing.
    /// </summary>
    /// <param name="obj">The node.</param>
    /// <returns>Never returns.</returns>
    public int GetHashCode(SearchNode obj) =>
        throw new NotSupportedException("Structural comparison for assertions only; hashing is not supported.");

    /// <summary>
    /// Order-sensitive element-wise comparison.
    /// </summary>
    /// <param name="x">The first sequence.</param>
    /// <param name="y">The second sequence.</param>
    /// <returns><see langword="true"/> when both sequences match element-wise.</returns>
    private static bool SequencesEqual(IReadOnlyList<SearchNode> x, IReadOnlyList<SearchNode> y) {
        if (x.Count != y.Count) {
            return false;
        }
        return x.Zip(y).All(pair => Instance.Equals(pair.First, pair.Second));
    }
}
