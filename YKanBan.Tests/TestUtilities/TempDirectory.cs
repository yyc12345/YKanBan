namespace YKanBan.Tests.TestUtilities;

/// <summary>
/// Creates a unique temporary directory per test instance and removes it on
/// disposal. The removal is retried once because SQLite WAL sidecar files can
/// linger for a moment after the connections close.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    /// <summary>
    /// Gets the unique directory path managed by this instance.
    /// </summary>
    public string FullPath { get; }

    public TempDirectory()
    {
        FullPath = Path.Combine(Path.GetTempPath(), "ykanban-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(FullPath);
    }

    /// <summary>
    /// Deletes the directory tree, retrying once after a short delay.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (IOException)
        {
            Thread.Sleep(100);
            try
            {
                Directory.Delete(FullPath, recursive: true);
            }
            catch (IOException)
            {
                // Leftovers in the OS temp folder are harmless; never fail a test over cleanup.
            }
        }
    }
}
