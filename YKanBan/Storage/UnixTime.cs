namespace YKanBan.Storage;

/// <summary>
/// Supplies the current Unix timestamp. Every timestamp persisted by YKanBan
/// is an INTEGER count of Unix seconds.
/// </summary>
public static class UnixTime {
    /// <summary>
    /// Gets the current UTC time expressed as Unix seconds.
    /// </summary>
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
