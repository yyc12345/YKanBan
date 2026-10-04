namespace YKanBan.Storage.Workspace;

/// <summary>
/// Exclusive, OS-enforced lock over a workspace's ykanban.lock. The handle is
/// held until disposal, so crashes and power loss release it automatically. The
/// lock file is kept empty; only its exclusive OS handle carries meaning.
/// </summary>
public sealed class WorkspaceLock : IDisposable
{
    private readonly FileStream _stream;

    /// <summary>
    /// Initializes the lock around an already-acquired handle.
    /// </summary>
    /// <param name="stream">The exclusively opened lock file stream.</param>
    /// <param name="lockFilePath">Path of the lock file being held.</param>
    private WorkspaceLock(FileStream stream, string lockFilePath)
    {
        _stream = stream;
        LockFilePath = lockFilePath;
    }

    /// <summary>
    /// Gets the path of the lock file being held.
    /// </summary>
    public string LockFilePath { get; }

    /// <summary>
    /// Acquires the workspace lock, leaving the lock file empty.
    /// </summary>
    /// <param name="folderPath">The workspace folder to lock.</param>
    /// <returns>The held lock; dispose it to release.</returns>
    /// <exception cref="WorkspaceNotInitializedException">The folder has no .ykanban structure.</exception>
    /// <exception cref="WorkspaceLockException">Another instance already holds the lock.</exception>
    public static WorkspaceLock Acquire(string folderPath)
    {
        string lockFilePath = WorkspacePaths.LockFile(folderPath);
        FileStream stream;
        try
        {
            // FileShare.None gives the exclusive semantics; FileMode.Create empties any stale file.
            stream = new FileStream(lockFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        }
        catch (DirectoryNotFoundException)
        {
            throw new WorkspaceNotInitializedException(folderPath);
        }
        catch (IOException ex)
        {
            throw new WorkspaceLockException(lockFilePath, ex);
        }

        return new WorkspaceLock(stream, lockFilePath);
    }

    /// <summary>
    /// Releases the exclusive handle. The lock file itself is intentionally
    /// left on disk; the OS releases the handle even on a crash.
    /// </summary>
    public void Dispose() => _stream.Dispose();
}
