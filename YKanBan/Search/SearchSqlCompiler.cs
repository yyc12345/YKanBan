namespace YKanBan.Search;

/// <summary>A compiled search predicate: a SQL fragment over the "c" (cards) alias plus its bound parameters.</summary>
/// <param name="Predicate">SQL boolean expression; intended for "SELECT ... FROM cards c WHERE {Predicate}".</param>
/// <param name="Parameters">Named parameters referenced by the predicate, in order.</param>
public sealed record CompiledSearch(string Predicate, IReadOnlyList<(string Name, object Value)> Parameters);

/// <summary>
/// Compiles a parsed search AST into a parameterized SQL predicate.
/// Matching is case-insensitive with SQLite's built-in ASCII semantics:
/// LIKE's default case folding plus COLLATE NOCASE for exact matches.
/// LIKE wildcards and the escape character in user text are escaped so they
/// match literally.
/// </summary>
public static class SearchSqlCompiler
{
    /// <summary>Compiles the query into a SQL predicate.</summary>
    public static CompiledSearch Compile(SearchNode query)
    {
        var parameters = new List<(string Name, object Value)>();
        string predicate = Build(query, parameters);
        return new CompiledSearch(predicate, parameters);
    }

    /// <summary>Recursively builds the SQL expression for one node.</summary>
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
                string titleParam = AddParameter(parameters, "%" + EscapeLike(freeText.Text) + "%");
                string contentParam = AddParameter(parameters, "%" + EscapeLike(freeText.Text) + "%");
                return $"(c.title LIKE {titleParam} ESCAPE '\\' OR c.content LIKE {contentParam} ESCAPE '\\')";
            }

            case SearchNode.FieldText field:
            {
                string column = field.Field == SearchField.Title ? "c.title" : "c.content";
                string param = AddParameter(parameters, "%" + EscapeLike(field.Text) + "%");
                return $"{column} LIKE {param} ESCAPE '\\'";
            }

            case SearchNode.TagName tag:
            {
                string param = AddParameter(parameters, tag.Name);
                return "EXISTS (SELECT 1 FROM card_tags ct JOIN tags t ON t.id = ct.tag_id " +
                       $"WHERE ct.card_id = c.id AND t.name COLLATE NOCASE = {param})";
            }

            case SearchNode.ColumnTitle column:
            {
                string param = AddParameter(parameters, column.Title);
                return $"c.column_id IN (SELECT id FROM columns WHERE title COLLATE NOCASE = {param})";
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

    /// <summary>Binds a value under a fresh name and returns the SQL placeholder.</summary>
    private static string AddParameter(List<(string Name, object Value)> parameters, object value)
    {
        string name = "$p" + parameters.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        parameters.Add((name, value));
        return name;
    }

    /// <summary>Escapes LIKE wildcards and the escape character inside user text.</summary>
    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
