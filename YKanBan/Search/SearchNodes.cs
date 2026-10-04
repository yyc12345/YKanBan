// YYC MARK: C# records synthesize equality by running EqualityComparer<T>.Default
// on each positional property. For collection properties (IReadOnlyList<>,
// ImmutableArray<>, arrays, ...) that comparer falls back to REFERENCE equality,
// so the Or/And nodes below deliberately do NOT compare structurally. The only
// structural comparison lives in the test helper (SearchNodeComparer); production
// code never compares AST nodes.
namespace YKanBan.Search;

/// <summary>
/// Root of the search query AST. Search keywords are fixed English grammar
/// and are never localized.
/// </summary>
public abstract record SearchNode {
    /// <summary>
    /// Disjunction: matches when any term matches (OR).
    /// </summary>
    /// <param name="Terms">The alternative terms.</param>
    public sealed record Or(IReadOnlyList<SearchNode> Terms) : SearchNode;

    /// <summary>
    /// Conjunction: matches when all terms match (explicit AND or adjacency).
    /// </summary>
    /// <param name="Terms">The required terms.</param>
    public sealed record And(IReadOnlyList<SearchNode> Terms) : SearchNode;

    /// <summary>
    /// A bare word or phrase matched as a substring of the title OR content.
    /// </summary>
    /// <param name="Text">The search text.</param>
    public sealed record FreeText(string Text) : SearchNode;

    /// <summary>
    /// A substring match scoped to one card field.
    /// </summary>
    /// <param name="Field">The card field to match against.</param>
    /// <param name="Text">The search text.</param>
    public sealed record FieldText(SearchField Field, string Text) : SearchNode;

    /// <summary>
    /// An exact, case-insensitive tag name match.
    /// </summary>
    /// <param name="Name">The tag name to match.</param>
    public sealed record TagName(string Name) : SearchNode;

    /// <summary>
    /// An exact, case-insensitive column title match.
    /// </summary>
    /// <param name="Title">The column title to match.</param>
    public sealed record ColumnTitle(string Title) : SearchNode;

    /// <summary>
    /// An exact card id match.
    /// </summary>
    /// <param name="Id">The card id to match.</param>
    public sealed record CardId(long Id) : SearchNode;
}

/// <summary>
/// The card fields a <see cref="SearchNode.FieldText"/> atom can scope to.
/// </summary>
public enum SearchField {
    /// <summary>
    /// The card title.
    /// </summary>
    Title,

    /// <summary>
    /// The card content body.
    /// </summary>
    Content,
}
