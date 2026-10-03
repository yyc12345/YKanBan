using Pidgin;

namespace YKanBan.Search;

/// <summary>
/// Lexical tokens of the search syntax.
/// </summary>
public abstract record SearchToken
{
    /// <summary>
    /// A bare word; qualifier detection happens later from the text.
    /// </summary>
    /// <param name="Text">The word text.</param>
    public sealed record Word(string Text) : SearchToken;

    /// <summary>
    /// A quoted phrase with its escapes already resolved.
    /// </summary>
    /// <param name="Text">The unescaped phrase content.</param>
    public sealed record Phrase(string Text) : SearchToken;

    /// <summary>
    /// An opening parenthesis.
    /// </summary>
    public sealed record OpenParen : SearchToken;

    /// <summary>
    /// A closing parenthesis.
    /// </summary>
    public sealed record CloseParen : SearchToken;
}

/// <summary>
/// Pidgin-based lexer for the search syntax. Splits the input into words,
/// quoted phrases and parentheses; whitespace only separates tokens.
///
/// The lexer is strict: inside a phrase only <c>\"</c> and <c>\\</c> are valid
/// escapes, a bare backslash or an unknown escape is rejected, a backslash in a
/// bare word is rejected, and an unterminated phrase is rejected. A phrase
/// must not be empty and must be separated from neighbouring words and
/// phrases by whitespace or a parenthesis, except as a qualifier value glued to
/// its key (<c>tag:"a b"</c>). Any input not covered by the token rules is
/// rejected rather than silently ignored.
/// </summary>
public static class SearchLexer
{
    /// <summary>A valid phrase escape sequence: <c>\"</c> or <c>\\</c>.</summary>
    private static readonly Parser<char, char> PhraseEscape =
        Parser.Try(Parser.String("\\\"").Select(_ => '"'))
            .Or(Parser.String("\\\\").Select(_ => '\\'));

    /// <summary>One phrase character: an escape or any character except a quote or backslash.</summary>
    private static readonly Parser<char, char> PhraseCharacter =
        PhraseEscape.Or(Parser<char>.Token(character => character != '"' && character != '\\'));

    private static readonly Parser<char, SearchToken> PhraseParser =
        from openingQuote in Parser.Char('"')
        from content in PhraseCharacter.Many()
        from closingQuote in Parser.Char('"')
        select (SearchToken)new SearchToken.Phrase(new string(content.ToArray()));

    private static readonly Parser<char, SearchToken> OpenParenParser =
        Parser.Char('(').Select(_ => (SearchToken)new SearchToken.OpenParen());

    private static readonly Parser<char, SearchToken> CloseParenParser =
        Parser.Char(')').Select(_ => (SearchToken)new SearchToken.CloseParen());

    // ':' stays inside the word token (the parser splits qualifiers); a backslash
    // is not a word character, so a bare one is left over and rejected by End.
    private static readonly Parser<char, SearchToken> WordParser =
        Parser<char>.Token(character =>
                !char.IsWhiteSpace(character) && character != '(' && character != ')' && character != '"' && character != '\\')
            .AtLeastOnce()
            .Select(chars => (SearchToken)new SearchToken.Word(new string(chars.ToArray())));

    // Each token with its source span, so adjacency can be checked afterwards.
    private static readonly Parser<char, PositionedToken> TokenParser =
        Parser.SkipWhitespaces.Then(
            from start in Parser<char>.CurrentOffset
            from token in PhraseParser
                .Or(OpenParenParser)
                .Or(CloseParenParser)
                .Or(WordParser)
            from end in Parser<char>.CurrentOffset
            select new PositionedToken(token, start, end));

    // Try(...) keeps a failed token non-consuming so Many stops gracefully; End
    // then rejects any leftover input, which turns dangling quotes or invalid
    // escapes into hard failures instead of silently dropped text.
    private static readonly Parser<char, IReadOnlyList<PositionedToken>> Lexer =
        Parser.Try(TokenParser).Many()
            .Before(Parser.SkipWhitespaces)
            .Before(Parser<char>.End)
            .Select(tokens => (IReadOnlyList<PositionedToken>)tokens.ToList());

    /// <summary>
    /// Tokenizes the search text.
    /// </summary>
    /// <param name="input">The raw search text.</param>
    /// <returns>The token stream.</returns>
    /// <exception cref="SearchSyntaxException">
    /// The text contains an invalid escape, an unterminated or empty phrase, a
    /// phrase glued to other text, or whitespace between a qualifier and its phrase value.
    /// </exception>
    public static IReadOnlyList<SearchToken> Tokenize(string input)
    {
        IReadOnlyList<PositionedToken> tokens;
        try
        {
            tokens = Lexer.ParseOrThrow(input);
        }
        catch (ParseException exception)
        {
            throw new SearchSyntaxException(exception.Message, exception);
        }

        EnsurePhrasesAreDelimited(tokens);
        return tokens.Select(positioned => positioned.Token).ToList();
    }

    /// <summary>
    /// Rejects the phrase placements the grammar does not allow but the token
    /// rules alone would accept: an empty phrase, a phrase touching a word or
    /// another phrase (<c>a"b"</c>, <c>"a"b</c>, <c>"a""b"</c>), and whitespace
    /// between <c>key:</c> and its phrase value (<c>tag: "x"</c>). The only
    /// legal contact is <c>key:"value"</c>.
    /// </summary>
    /// <param name="tokens">The positioned token stream.</param>
    /// <exception cref="SearchSyntaxException">A phrase is misplaced.</exception>
    private static void EnsurePhrasesAreDelimited(IReadOnlyList<PositionedToken> tokens)
    {
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Token is SearchToken.Phrase { Text.Length: 0 })
            {
                throw new SearchSyntaxException("Empty phrase \"\" is not allowed.");
            }

            if (index == 0)
            {
                continue;
            }

            PositionedToken previous = tokens[index - 1];
            PositionedToken current = tokens[index];
            bool qualifierValue = previous.Token is SearchToken.Word word && word.Text.EndsWith(':')
                && current.Token is SearchToken.Phrase;
            bool touching = previous.End == current.Start;

            if (qualifierValue && !touching)
            {
                throw new SearchSyntaxException(
                    $"Qualifier '{((SearchToken.Word)previous.Token).Text}' must be followed directly by its value, without whitespace.");
            }

            bool phraseInvolved = previous.Token is SearchToken.Phrase || current.Token is SearchToken.Phrase;
            bool parenInvolved = previous.Token is SearchToken.OpenParen or SearchToken.CloseParen
                || current.Token is SearchToken.OpenParen or SearchToken.CloseParen;
            if (touching && phraseInvolved && !parenInvolved && !qualifierValue)
            {
                throw new SearchSyntaxException("A phrase must be separated from adjacent text by whitespace.");
            }
        }
    }

    /// <summary>
    /// A token with its source span.
    /// </summary>
    /// <param name="Token">The token.</param>
    /// <param name="Start">Offset of the first character.</param>
    /// <param name="End">Offset just past the last character.</param>
    private sealed record PositionedToken(SearchToken Token, int Start, int End);
}
