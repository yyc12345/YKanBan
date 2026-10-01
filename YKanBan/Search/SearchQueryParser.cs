using System.Globalization;
using Pidgin;

namespace YKanBan.Search;

/// <summary>
/// Thrown when the search text violates the search grammar. Carries the
/// underlying parser's descriptive message (including the error position);
/// the UI surfaces it as a search error instead of coupling to Pidgin types.
/// </summary>
public sealed class SearchSyntaxException : Exception
{
    /// <summary>Creates an exception wrapping the original parse failure.</summary>
    public SearchSyntaxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Parses the search grammar with Pidgin, strictly: any malformed input
/// (unbalanced parentheses, dangling operators, unterminated phrases,
/// qualifier keys without a value, non-numeric id values) raises
/// <see cref="SearchSyntaxException"/>. Only uppercase AND/OR are operators
/// (lowercase ones are plain words); AND — explicit or by adjacency — binds
/// tighter than OR; parentheses group. Blank input returns null and means
/// "show everything".
/// </summary>
public static class SearchQueryParser
{
    /// <summary>Recognized qualifier kinds; extend <see cref="Qualifiers"/> to add new keys.</summary>
    private enum QualifierKind
    {
        Tag,
        Title,
        Content,
        Id,
        Column,
    }

    /// <summary>The qualifier key table ("key:" prefixes); reserved for future keys like created:.</summary>
    private static readonly (string Key, QualifierKind Kind)[] Qualifiers =
    [
        ("tag", QualifierKind.Tag),
        ("title", QualifierKind.Title),
        ("content", QualifierKind.Content),
        ("id", QualifierKind.Id),
        ("column", QualifierKind.Column),
    ];

    #region Token primitives

    private static readonly Parser<SearchToken, SearchToken> AndKeyword =
        Parser<SearchToken>.Token(new SearchToken.Word("AND"));

    private static readonly Parser<SearchToken, SearchToken> OrKeyword =
        Parser<SearchToken>.Token(new SearchToken.Word("OR"));

    private static readonly Parser<SearchToken, SearchToken> OpenParen =
        Parser<SearchToken>.Token((SearchToken)new SearchToken.OpenParen());

    private static readonly Parser<SearchToken, SearchToken> CloseParen =
        Parser<SearchToken>.Token((SearchToken)new SearchToken.CloseParen());

    /// <summary>The text of a phrase token.</summary>
    private static readonly Parser<SearchToken, string> PhraseText =
        Parser<SearchToken>.Token(t => t is SearchToken.Phrase)
            .Select(t => ((SearchToken.Phrase)t).Text);

    /// <summary>The text of a word token, operator keywords excluded (they never start an operand).</summary>
    private static readonly Parser<SearchToken, string> NonKeywordWord =
        Parser<SearchToken>.Token(t => t is SearchToken.Word w && w.Text != "AND" && w.Text != "OR")
            .Select(t => ((SearchToken.Word)t).Text);

    #endregion

    #region Grammar

    // A bare phrase matched against title or content.
    private static readonly Parser<SearchToken, SearchNode> FreePhrase =
        PhraseText.Select(text => (SearchNode)new SearchNode.FreeText(text));

    // A bare word: a "key:value" qualifier when the prefix matches, otherwise free text.
    private static readonly Parser<SearchToken, SearchNode> WordAtom =
        NonKeywordWord.Bind(QualifierFrom);

    // '(' orExpr ')' — the deferred method call breaks the
    // Parenthesized → OrExpr → AndExpr → Operand → Parenthesized recursion cycle.
    private static readonly Parser<SearchToken, SearchNode> Parenthesized =
        OpenParen.Then(_ => OrExprDeferred()).Before(CloseParen);

    // operand := '(' orExpr ')' | qualifier-or-word | phrase
    private static readonly Parser<SearchToken, SearchNode> Operand =
        Parenthesized.Or(WordAtom).Or(FreePhrase);

    // One andExpr operand together with its optional explicit AND keyword.
    private static readonly Parser<SearchToken, SearchNode> AndPrefixedOperand =
        AndKeyword.Optional().Then(Operand);

    // andExpr := operand (AND? operand)*
    private static readonly Parser<SearchToken, SearchNode> AndExpr =
        Operand.Bind(first =>
            AndPrefixedOperand.Many()
                .Select(rest => FoldAnd(first, rest)));

    // orExpr := andExpr (OR andExpr)+
    private static readonly Parser<SearchToken, SearchNode> OrExpr =
        AndExpr.SeparatedAtLeastOnce(OrKeyword)
            .Select(FoldOr);

    // The full query: orExpr consuming the whole token stream.
    private static readonly Parser<SearchToken, SearchNode> Query =
        OrExpr.Before(Parser<SearchToken>.End);

    /// <summary>Defers the OrExpr field read to parse time (recursion cycle breaker).</summary>
    private static Parser<SearchToken, SearchNode> OrExprDeferred() => OrExpr;

    #endregion

    /// <summary>Parses a search box text; returns null for blank input (meaning: show everything).</summary>
    /// <exception cref="SearchSyntaxException">The text violates the grammar.</exception>
    public static SearchNode? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            List<SearchToken> tokens = SearchLexer.Tokenize(text).ToList();
            return Query.ParseOrThrow(tokens);
        }
        catch (ParseException ex)
        {
            throw new SearchSyntaxException(ex.Message, ex);
        }
    }

    /// <summary>Builds the parser for one word operand: qualifier atom or free text.</summary>
    private static Parser<SearchToken, SearchNode> QualifierFrom(string word)
    {
        foreach ((string key, QualifierKind kind) in Qualifiers)
        {
            // Qualifier keys are case-sensitive lowercase grammar tokens.
            if (!word.StartsWith(key + ":", StringComparison.Ordinal))
            {
                continue;
            }
            string value = word[(key.Length + 1)..];

            // "key:" without an inline value takes the value from the following phrase, e.g. tag:"a b".
            return value.Length == 0
                ? PhraseText.Bind(text => AtomParser(kind, text))
                : AtomParser(kind, value);
        }

        // No qualifier prefix: a plain free-text word.
        return Parser<SearchToken>.Return<SearchNode>(new SearchNode.FreeText(word));
    }

    /// <summary>Builds the atom parser for a qualifier value, failing strictly on a non-numeric id.</summary>
    private static Parser<SearchToken, SearchNode> AtomParser(QualifierKind kind, string value)
    {
        return AtomFrom(kind, value) is { } atom
            ? Parser<SearchToken>.Return(atom)
            : Parser<SearchToken>.Fail<SearchNode>($"id: expects an integer, got '{value}'.");
    }

    /// <summary>Builds the atom for a qualifier value; null when an id value is not an integer.</summary>
    private static SearchNode? AtomFrom(QualifierKind kind, string value) => kind switch
    {
        QualifierKind.Tag => new SearchNode.TagName(value),
        QualifierKind.Title => new SearchNode.FieldText(SearchField.Title, value),
        QualifierKind.Content => new SearchNode.FieldText(SearchField.Content, value),
        QualifierKind.Column => new SearchNode.ColumnTitle(value),
        QualifierKind.Id when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) => new SearchNode.CardId(id),
        _ => null,
    };

    /// <summary>Collapses an AND chain into a single node.</summary>
    private static SearchNode FoldAnd(SearchNode first, IEnumerable<SearchNode> rest)
    {
        List<SearchNode> terms = [first, .. rest];
        return terms.Count == 1 ? terms[0] : new SearchNode.And(terms);
    }

    /// <summary>Collapses an OR chain into a single node.</summary>
    private static SearchNode FoldOr(IEnumerable<SearchNode> terms)
    {
        List<SearchNode> list = terms.ToList();
        return list.Count == 1 ? list[0] : new SearchNode.Or(list);
    }
}
