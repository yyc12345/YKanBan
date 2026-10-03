using YKanBan.Storage;

namespace YKanBan.Search;

/// <summary>
/// A compiled search predicate: a SQL fragment over the "c" (cards) alias plus
/// its bound parameters.
/// </summary>
/// <param name="Predicate">SQL boolean expression, for <c>SELECT ... FROM cards c WHERE {Predicate}</c>.</param>
/// <param name="Parameters">Named parameters referenced by the predicate, in order.</param>
public sealed record CompiledSearch(string Predicate, IReadOnlyList<(string Name, object Value)> Parameters);

/// <summary>
/// Compiles a parsed search AST into a parameterized SQL predicate. Matching is
/// Unicode case-insensitive: both sides are folded with
/// <see cref="SqliteDatabase.Fold"/> (the column side through the per-connection
/// <see cref="SqliteDatabase.FoldFunctionName"/> function) and then compared
/// ordinally — <c>instr</c> for substrings, <c>=</c> for exact matches. LIKE is
/// deliberately not used, so <c>%</c> and <c>_</c> carry no wildcard meaning.
/// </summary>
public static class SearchSqlCompiler
{
    /// <summary>
    /// Compiles the query into a SQL predicate.
    /// </summary>
    /// <param name="query">The parsed search AST.</param>
    /// <returns>The compiled predicate and its parameters.</returns>
    public static CompiledSearch Compile(SearchNode query)
    {
        var parameters = new List<(string Name, object Value)>();
        string predicate = Build(query, parameters);
        return new CompiledSearch(predicate, parameters);
    }

    /// <summary>
    /// Recursively builds the SQL expression for one node.
    /// </summary>
    /// <param name="node">The AST node to compile.</param>
    /// <param name="parameters">The parameter accumulator.</param>
    /// <returns>The SQL boolean expression for the node.</returns>
    private static string Build(SearchNode node, List<(string Name, object Value)> parameters)
    {
        switch (node)
        {
            case SearchNode.And and:
                return "(" + string.Join(" AND ", and.Terms.Select(term => Build(term, parameters))) + ")";

            case SearchNode.Or or:
                return "(" + string.Join(" OR ", or.Terms.Select(term => Build(term, parameters))) + ")";

            case SearchNode.FreeText freeText:
            {
                // A bare word matches a substring of the title OR the content.
                string param = AddParameter(parameters, SqliteDatabase.Fold(freeText.Text));
                return $"({Contains("c.title", param)} OR {Contains("c.content", param)})";
            }

            case SearchNode.FieldText field:
            {
                string column = field.Field == SearchField.Title ? "c.title" : "c.content";
                string param = AddParameter(parameters, SqliteDatabase.Fold(field.Text));
                return Contains(column, param);
            }

            case SearchNode.TagName tag:
            {
                string param = AddParameter(parameters, SqliteDatabase.Fold(tag.Name));
                return "EXISTS (SELECT 1 FROM card_tags ct JOIN tags t ON t.id = ct.tag_id " +
                       $"WHERE ct.card_id = c.id AND {Folded("t.name")} = {param})";
            }

            case SearchNode.ColumnTitle column:
            {
                string param = AddParameter(parameters, SqliteDatabase.Fold(column.Title));
                return $"c.column_id IN (SELECT id FROM columns WHERE {Folded("title")} = {param})";
            }

            case SearchNode.CardId id:
            {
                string param = AddParameter(parameters, id.Id);
                return $"c.id = {param}";
            }

            default:
                throw new InvalidOperationException($"Unsupported search node: {node.GetType().Name}");
        }
    }

    /// <summary>
    /// Binds a value under a fresh name and returns the SQL placeholder.
    /// </summary>
    /// <param name="parameters">The parameter accumulator.</param>
    /// <param name="value">The value to bind.</param>
    /// <returns>The placeholder text.</returns>
    private static string AddParameter(List<(string Name, object Value)> parameters, object value)
    {
        string name = "$p" + parameters.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        parameters.Add((name, value));
        return name;
    }

    /// <summary>
    /// Wraps a column expression in the case-folding SQL function.
    /// </summary>
    /// <param name="column">The column expression.</param>
    /// <returns>The folded column expression.</returns>
    private static string Folded(string column) => $"{SqliteDatabase.FoldFunctionName}({column})";

    /// <summary>
    /// Builds an ordinal substring test of a folded column against an already folded parameter.
    /// </summary>
    /// <param name="column">The column expression.</param>
    /// <param name="param">The placeholder of the folded search text.</param>
    /// <returns>The SQL boolean expression.</returns>
    private static string Contains(string column, string param) => $"instr({Folded(column)}, {param}) > 0";
}
