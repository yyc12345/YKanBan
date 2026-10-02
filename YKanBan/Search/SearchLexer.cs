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
/// escapes, a bare backslash or an unknown escape is rejected, and an
/// unterminated phrase is rejected. Any input not covered by the token rules
/// is rejected rather than silently ignored.
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

    private static readonly Parser<char, SearchToken> WordParser =
        Parser<char>.Token(character =>
                !char.IsWhiteSpace(character) && character != '(' && character != ')' && character != '"')
            .AtLeastOnce()
            .Select(chars => (SearchToken)new SearchToken.Word(new string(chars.ToArray())));

    private static readonly Parser<char, SearchToken> TokenParser =
        Parser.SkipWhitespaces.Then(
            PhraseParser
                .Or(OpenParenParser)
                .Or(CloseParenParser)
                .Or(WordParser));

    // Try(...) keeps a failed token non-consuming so Many stops gracefully; End
    // then rejects any leftover input, which turns dangling quotes or invalid
    // escapes into hard failures instead of silently dropped text.
    private static readonly Parser<char, IReadOnlyList<SearchToken>> Lexer =
        Parser.Try(TokenParser).Many()
            .Before(Parser.SkipWhitespaces)
            .Before(Parser<char>.End)
            .Select(tokens => (IReadOnlyList<SearchToken>)tokens.ToList());

    /// <summary>
    /// Tokenizes the search text.
    /// </summary>
    /// <param name="input">The raw search text.</param>
    /// <returns>The token stream.</returns>
    /// <exception cref="SearchSyntaxException">The text contains an invalid escape or an unterminated phrase.</exception>
    public static IReadOnlyList<SearchToken> Tokenize(string input)
    {
        try
        {
            return Lexer.ParseOrThrow(input);
        }
        catch (ParseException exception)
        {
            throw new SearchSyntaxException(exception.Message, exception);
        }
    }
}
