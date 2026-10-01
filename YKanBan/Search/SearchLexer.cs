using Pidgin;

namespace YKanBan.Search;

/// <summary>Lexical tokens of the search syntax.</summary>
public abstract record SearchToken
{
    /// <summary>A bare word; qualifier detection happens later from the text.</summary>
    public sealed record Word(string Text) : SearchToken;

    /// <summary>A quoted phrase with escapes already resolved.</summary>
    public sealed record Phrase(string Text) : SearchToken;

    /// <summary>An opening parenthesis.</summary>
    public sealed record OpenParen : SearchToken;

    /// <summary>A closing parenthesis.</summary>
    public sealed record CloseParen : SearchToken;
}

/// <summary>
/// Pidgin-based lexer for the search syntax. Splits the input into words,
/// quoted phrases (with \" and \\ escapes) and parentheses; whitespace only
/// separates tokens. The lexer is total: it never rejects input — anything
/// the dedicated rules do not cover falls through to the word rule, and an
/// unterminated phrase simply extends to the end of the input.
/// </summary>
public static class SearchLexer
{
    private static readonly Parser<char, char> PhraseCharacter =
        Parser.Try(Parser.String("\\\"").Select(_ => '"'))
            .Or(Parser.Try(Parser.String("\\\\").Select(_ => '\\')))
            .Or(Parser.AnyCharExcept('"'));

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
        Parser<char>.Token(c => !char.IsWhiteSpace(c) && c != '(' && c != ')' && c != '"')
            .AtLeastOnce()
            .Select(chars => (SearchToken)new SearchToken.Word(new string(chars.ToArray())));

    private static readonly Parser<char, SearchToken> AnyCharacterAsWord =
        Parser<char>.Any.Select(c => (SearchToken)new SearchToken.Word(c.ToString()));

    private static readonly Parser<char, SearchToken> TokenParser =
        Parser.SkipWhitespaces.Then(
            PhraseParser
                .Or(OpenParenParser)
                .Or(CloseParenParser)
                .Or(WordParser)
                .Or(AnyCharacterAsWord));

    private static readonly Parser<char, IReadOnlyList<SearchToken>> Lexer =
        // Try(...) keeps a trailing-whitespace failure non-consuming so Many stops
        // gracefully; the trailing Before(...) then swallows the leftover whitespace.
        Parser.Try(TokenParser).Many()
            .Before(Parser.SkipWhitespaces)
            .Select(tokens => (IReadOnlyList<SearchToken>)tokens.ToList());

    /// <summary>Tokenizes the search text; total over any input.</summary>
    public static IReadOnlyList<SearchToken> Tokenize(string input) =>
        Lexer.ParseOrThrow(input);
}
