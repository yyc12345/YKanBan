using YKanBan.Search;

namespace YKanBan.Tests.Search;

/// <summary>
/// Tests for the search parser: the full grammar branches plus the strict
/// failure behavior (malformed input always throws
/// <see cref="SearchSyntaxException"/>).
/// </summary>
[TestClass]
public class SearchQueryParserTests
{
    /// <summary>
    /// Parses and asserts structural equality against the expected AST.
    /// </summary>
    /// <param name="expected">The expected AST.</param>
    /// <param name="text">The search text.</param>
    private static void AssertParsesTo(SearchNode expected, string text)
    {
        SearchNode? parsed = SearchQueryParser.Parse(text);
        Assert.IsNotNull(parsed, $"Blank-looking input: '{text}'.");
        Assert.AreEqual(expected, parsed, SearchNodeComparer.Instance);
    }

    /// <summary>
    /// Asserts that the input violates the grammar.
    /// </summary>
    /// <param name="text">The search text.</param>
    private static void AssertSyntaxError(string text)
    {
        Assert.ThrowsExactly<SearchSyntaxException>(() => SearchQueryParser.Parse(text));
    }

    #region Successful parses

    [TestMethod]
    public void BlankInputReturnsNull()
    {
        Assert.IsNull(SearchQueryParser.Parse(string.Empty));
        Assert.IsNull(SearchQueryParser.Parse("   "));
    }

    [TestMethod]
    public void SingleWordIsFreeText()
    {
        AssertParsesTo(new SearchNode.FreeText("login"), "login");
    }

    [TestMethod]
    public void AdjacentWordsAreImplicitAnd()
    {
        AssertParsesTo(
            new SearchNode.And([new SearchNode.FreeText("fix"), new SearchNode.FreeText("login")]),
            "fix login");
    }

    [TestMethod]
    public void ExplicitAndKeyword()
    {
        AssertParsesTo(
            new SearchNode.And([new SearchNode.FreeText("a"), new SearchNode.FreeText("b")]),
            "a AND b");
    }

    [TestMethod]
    public void OrKeyword()
    {
        AssertParsesTo(
            new SearchNode.Or([new SearchNode.FreeText("a"), new SearchNode.FreeText("b")]),
            "a OR b");
    }

    [TestMethod]
    public void ImplicitAndBindsTighterThanOr()
    {
        AssertParsesTo(
            new SearchNode.Or([
                new SearchNode.And([new SearchNode.FreeText("a"), new SearchNode.FreeText("b")]),
                new SearchNode.FreeText("c")]),
            "a b OR c");
    }

    [TestMethod]
    public void ParenthesesForceGrouping()
    {
        AssertParsesTo(
            new SearchNode.And([
                new SearchNode.FreeText("a"),
                new SearchNode.Or([new SearchNode.FreeText("b"), new SearchNode.FreeText("c")])]),
            "a (b OR c)");
    }

    [TestMethod]
    public void NestedParenthesesCollapse()
    {
        AssertParsesTo(
            new SearchNode.Or([new SearchNode.FreeText("a"), new SearchNode.FreeText("b")]),
            "((a OR b))");
    }

    [TestMethod]
    public void LowercaseKeywordsArePlainWords()
    {
        AssertParsesTo(
            new SearchNode.And([
                new SearchNode.FreeText("a"),
                new SearchNode.FreeText("and"),
                new SearchNode.FreeText("or"),
                new SearchNode.FreeText("b")]),
            "a and or b");
    }

    [TestMethod]
    public void PhraseIsFreeText()
    {
        AssertParsesTo(new SearchNode.FreeText("hello world"), "\"hello world\"");
    }

    [TestMethod]
    public void QuotedOperatorKeywordIsFreeText()
    {
        // A quoted AND/OR is literal text, not an operator.
        AssertParsesTo(new SearchNode.FreeText("AND"), "\"AND\"");
        AssertParsesTo(
            new SearchNode.And([new SearchNode.FreeText("a"), new SearchNode.FreeText("OR")]),
            "a \"OR\"");
    }

    [TestMethod]
    public void PhraseSupportsEscapes()
    {
        // Escaped quote and escaped backslash.
        AssertParsesTo(new SearchNode.FreeText("a\"b"), "\"a\\\"b\"");
        AssertParsesTo(new SearchNode.FreeText("a\\b"), "\"a\\\\b\"");
    }

    [TestMethod]
    public void TagQualifier()
    {
        AssertParsesTo(new SearchNode.TagName("bug"), "tag:bug");
    }

    [TestMethod]
    public void TagQualifierAcceptsPhraseValue()
    {
        AssertParsesTo(new SearchNode.TagName("a b"), "tag:\"a b\"");
    }

    [TestMethod]
    public void QualifierValueMayContainColons()
    {
        AssertParsesTo(new SearchNode.TagName("a:b"), "tag:a:b");
    }

    [TestMethod]
    public void TitleAndContentQualifiers()
    {
        AssertParsesTo(new SearchNode.FieldText(SearchField.Title, "foo"), "title:foo");
        AssertParsesTo(new SearchNode.FieldText(SearchField.Content, "foo"), "content:foo");
    }

    [TestMethod]
    public void ColumnQualifierAcceptsPhraseValue()
    {
        AssertParsesTo(new SearchNode.ColumnTitle("To Do"), "column:\"To Do\"");
    }

    [TestMethod]
    public void IdQualifierParsesNumber()
    {
        AssertParsesTo(new SearchNode.CardId(42), "id:42");
        AssertParsesTo(new SearchNode.CardId(12), "id:\"12\"");
    }

    [TestMethod]
    public void ColonWithoutKeyPrefixIsFreeText()
    {
        AssertParsesTo(new SearchNode.FreeText(":foo"), ":foo");
    }

    #endregion

    #region Strict failures

    [TestMethod]
    public void UnterminatedPhraseIsAnError()
    {
        AssertSyntaxError("\"abc def");
    }

    [TestMethod]
    public void TrailingUnterminatedPhraseIsAnError()
    {
        AssertSyntaxError("a \"abc");
    }

    [TestMethod]
    public void InvalidEscapeInPhraseIsAnError()
    {
        // A bare backslash (not followed by a quote or another backslash) is invalid.
        AssertSyntaxError("\"a\\b\"");
    }

    [TestMethod]
    public void InvalidEscapeInQualifierPhraseIsAnError()
    {
        AssertSyntaxError("tag:\"a\\x\"");
    }

    [TestMethod]
    public void UnbalancedOpenParenIsAnError()
    {
        AssertSyntaxError("(a");
    }

    [TestMethod]
    public void StrayCloseParenIsAnError()
    {
        AssertSyntaxError("a)");
    }

    [TestMethod]
    public void EmptyParensAreAnError()
    {
        AssertSyntaxError("()");
    }

    [TestMethod]
    public void DanglingOrIsAnError()
    {
        AssertSyntaxError("a OR");
    }

    [TestMethod]
    public void DanglingAndIsAnError()
    {
        AssertSyntaxError("a AND");
    }

    [TestMethod]
    public void LeadingAndIsAnError()
    {
        AssertSyntaxError("AND a");
    }

    [TestMethod]
    public void OperatorAtOperandPositionIsAnError()
    {
        AssertSyntaxError("a AND OR b");
    }

    [TestMethod]
    public void QualifierWithoutValueIsAnError()
    {
        AssertSyntaxError("tag:");
    }

    [TestMethod]
    public void SpacedQualifierValueIsAnError()
    {
        AssertSyntaxError("tag: bug");
    }

    [TestMethod]
    public void NonNumericIdIsAnError()
    {
        AssertSyntaxError("id:12x");
    }

    [TestMethod]
    public void UnknownQualifierKeyIsAnError()
    {
        AssertSyntaxError("foo:bar");
    }

    [TestMethod]
    public void QualifierKeysAreCaseSensitive()
    {
        // "Tag" is a key-like prefix but not the known lowercase "tag", so it is unknown.
        AssertSyntaxError("Tag:x");
    }

    [TestMethod]
    public void SyntaxErrorCarriesDescriptiveMessage()
    {
        SearchSyntaxException exception = Assert.ThrowsExactly<SearchSyntaxException>(
            () => SearchQueryParser.Parse("a AND"));
        Assert.IsFalse(string.IsNullOrEmpty(exception.Message));
    }

    #endregion
}
