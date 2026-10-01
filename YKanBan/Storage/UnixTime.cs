namespace YKanBan.Storage;

/// <summary>Provides the current Unix time; all persisted timestamps are INTEGER Unix seconds.</summary>
public static class UnixTime
{
    /// <summary>Current UTC time as Unix seconds.</summary>
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
