namespace YKanBan.Tests.TestUtilities;

/// <summary>
/// Creates a unique temporary directory per test instance and removes it on
/// disposal. Retries the deletion once because SQLite WAL files can linger
/// for a moment after connections close.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    /// <summary>Unique directory path managed by this instance.</summary>
    public string FullPath { get; }

    public TempDirectory()
    {
        FullPath = Path.Combine(Path.GetTempPath(), "ykanban-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(FullPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (IOException)
        {
            Thread.Sleep(100);
            try { Directory.Delete(FullPath, recursive: true); } catch (IOException) { }
        }
    }
}
