namespace YKanBan.Search;

/// <summary>
/// Thrown when the search text violates the search grammar. Carries the
/// underlying parser's descriptive English message; the UI surfaces it as a
/// search error instead of coupling to Pidgin types. The message is never
/// localized.
/// </summary>
public sealed class SearchSyntaxException : Exception
{
    /// <summary>
    /// Initializes the exception with a descriptive English message.
    /// </summary>
    /// <param name="message">The English diagnostic message.</param>
    /// <param name="innerException">The underlying parse failure.</param>
    public SearchSyntaxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
