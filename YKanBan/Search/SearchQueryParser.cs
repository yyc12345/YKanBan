using System.Globalization;
using Pidgin;

namespace YKanBan.Search;

/// <summary>
/// Parses the search grammar with Pidgin, strictly: any malformed input
/// (unbalanced parentheses, dangling or consecutive operators, unterminated
/// phrases, invalid escapes, qualifier keys without a value or with an
/// unknown/non-numeric id value) raises <see cref="SearchSyntaxException"/>.
///
/// Only uppercase AND/OR are operators (lowercase ones are plain words); AND —
/// explicit or by adjacency — binds tighter than OR; parentheses group. Blank
/// input returns null and means "show everything". Search keywords and
/// qualifier keys are fixed English grammar and are never localized.
/// </summary>
public static class SearchQueryParser
{
    /// <summary>The recognized qualifier keys; add new entries here to extend the grammar.</summary>
    private static readonly (string Key, QualifierKind Kind)[] QualifierTable =
    [
        ("tag", QualifierKind.Tag),
        ("title", QualifierKind.Title),
        ("content", QualifierKind.Content),
        ("id", QualifierKind.Id),
        ("column", QualifierKind.Column),
    ];

    #region Token primitives

    private static readonly Parser<SearchToken, SearchToken> AndKeyword =
        Parser<SearchToken>.Token(token => token is SearchToken.Word { Text: "AND" });

    private static readonly Parser<SearchToken, SearchToken> OrKeyword =
        Parser<SearchToken>.Token(token => token is SearchToken.Word { Text: "OR" });

    private static readonly Parser<SearchToken, SearchToken> OpenParen =
        Parser<SearchToken>.Token(token => token is SearchToken.OpenParen);

    private static readonly Parser<SearchToken, SearchToken> CloseParen =
        Parser<SearchToken>.Token(token => token is SearchToken.CloseParen);

    /// <summary>The text of a phrase token.</summary>
    private static readonly Parser<SearchToken, string> PhraseText =
        Parser<SearchToken>.Token(token => token is SearchToken.Phrase)
            .Select(token => ((SearchToken.Phrase)token).Text);

    /// <summary>The text of a word token, operator keywords excluded (they never start an operand).</summary>
    private static readonly Parser<SearchToken, string> NonKeywordWord =
        Parser<SearchToken>.Token(token => token is SearchToken.Word word && word.Text != "AND" && word.Text != "OR")
            .Select(token => ((SearchToken.Word)token).Text);

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
    /// <returns>The OR-expression parser.</returns>
    private static Parser<SearchToken, SearchNode> OrExprDeferred() => OrExpr;

    #endregion

    /// <summary>
    /// Parses a search box text.
    /// </summary>
    /// <param name="text">The raw search text.</param>
    /// <returns>The parsed query, or <see langword="null"/> for blank input (meaning: show everything).</returns>
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
        catch (ParseException exception)
        {
            throw new SearchSyntaxException(exception.Message, exception);
        }
    }

    #region Atom construction

    /// <summary>
    /// Builds the parser for one word operand: a qualifier atom, an unknown-key
    /// failure, or free text.
    /// </summary>
    /// <param name="word">The word token text.</param>
    /// <returns>The parser producing the operand node.</returns>
    private static Parser<SearchToken, SearchNode> QualifierFrom(string word)
    {
        int colonIndex = word.IndexOf(':');

        // Only a leading run of letters/digits/underscores before ':' can be a qualifier key.
        if (colonIndex <= 0 || !IsQualifierKeyCandidate(word.AsSpan(0, colonIndex)))
        {
            return Parser<SearchToken>.Return<SearchNode>(new SearchNode.FreeText(word));
        }

        string key = word[..colonIndex];
        string value = word[(colonIndex + 1)..];

        if (!TryLookupQualifier(key, out QualifierKind kind))
        {
            return Parser<SearchToken>.Fail<SearchNode>($"Unknown qualifier key '{key}'.");
        }

        // "key:" without an inline value takes the value from the following phrase, e.g. tag:"a b".
        return value.Length == 0
            ? PhraseText.Bind(text => AtomParser(kind, text))
            : AtomParser(kind, value);
    }

    /// <summary>
    /// Builds the atom parser for a qualifier value, failing strictly on a non-numeric id.
    /// </summary>
    /// <param name="kind">The qualifier kind.</param>
    /// <param name="value">The qualifier value.</param>
    /// <returns>The parser producing the atom node.</returns>
    private static Parser<SearchToken, SearchNode> AtomParser(QualifierKind kind, string value) =>
        AtomFrom(kind, value) is { } atom
            ? Parser<SearchToken>.Return(atom)
            : Parser<SearchToken>.Fail<SearchNode>($"The id qualifier expects an integer, got '{value}'.");

    /// <summary>
    /// Builds the atom for a qualifier value; null when an id value is not an integer.
    /// </summary>
    /// <param name="kind">The qualifier kind.</param>
    /// <param name="value">The qualifier value.</param>
    /// <returns>The atom node, or <see langword="null"/> for an invalid id value.</returns>
    private static SearchNode? AtomFrom(QualifierKind kind, string value) => kind switch
    {
        QualifierKind.Tag => new SearchNode.TagName(value),
        QualifierKind.Title => new SearchNode.FieldText(SearchField.Title, value),
        QualifierKind.Content => new SearchNode.FieldText(SearchField.Content, value),
        QualifierKind.Column => new SearchNode.ColumnTitle(value),
        QualifierKind.Id when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) =>
            new SearchNode.CardId(id),
        _ => null,
    };

    /// <summary>
    /// Looks up a qualifier key in the extension table with ordinal (case-sensitive) matching.
    /// </summary>
    /// <param name="key">The key text before the colon.</param>
    /// <param name="kind">The matching qualifier kind.</param>
    /// <returns><see langword="true"/> when the key is known.</returns>
    private static bool TryLookupQualifier(string key, out QualifierKind kind)
    {
        foreach ((string candidate, QualifierKind candidateKind) in QualifierTable)
        {
            if (string.Equals(candidate, key, StringComparison.Ordinal))
            {
                kind = candidateKind;
                return true;
            }
        }

        kind = default;
        return false;
    }

    /// <summary>
    /// Returns whether the text looks like a qualifier key: a non-empty run of
    /// letters, digits or underscores.
    /// </summary>
    /// <param name="text">The candidate key text.</param>
    /// <returns><see langword="true"/> when every character is a word character.</returns>
    private static bool IsQualifierKeyCandidate(ReadOnlySpan<char> text)
    {
        foreach (char character in text)
        {
            if (!(char.IsLetterOrDigit(character) || character == '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Collapses an AND chain into a single node.
    /// </summary>
    /// <param name="first">The first operand.</param>
    /// <param name="rest">The remaining operands.</param>
    /// <returns>The single operand, or an <see cref="SearchNode.And"/> node.</returns>
    private static SearchNode FoldAnd(SearchNode first, IEnumerable<SearchNode> rest)
    {
        List<SearchNode> terms = [first, .. rest];
        return terms.Count == 1 ? terms[0] : new SearchNode.And(terms);
    }

    /// <summary>
    /// Collapses an OR chain into a single node.
    /// </summary>
    /// <param name="terms">The operands.</param>
    /// <returns>The single operand, or a <see cref="SearchNode.Or"/> node.</returns>
    private static SearchNode FoldOr(IEnumerable<SearchNode> terms)
    {
        List<SearchNode> list = terms.ToList();
        return list.Count == 1 ? list[0] : new SearchNode.Or(list);
    }

    #endregion

    /// <summary>The recognized qualifier kinds.</summary>
    private enum QualifierKind
    {
        Tag,
        Title,
        Content,
        Id,
        Column,
    }
}
