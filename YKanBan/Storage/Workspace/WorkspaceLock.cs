using System.Text;

namespace YKanBan.Storage.Workspace;

/// <summary>
/// Exclusive, OS-enforced lock over a workspace's ykanban.lock. The handle
/// is held until disposal; crashes and power loss release it automatically.
/// The lock file carries PID / machine name / time for human diagnostics
/// only.
/// </summary>
public sealed class WorkspaceLock : IDisposable
{
    private readonly FileStream _stream;

    private WorkspaceLock(FileStream stream, string lockFilePath)
    {
        _stream = stream;
        LockFilePath = lockFilePath;
    }

    /// <summary>Path of the lock file being held.</summary>
    public string LockFilePath { get; }

    /// <summary>Acquires the workspace lock or throws <see cref="WorkspaceLockException"/>.</summary>
    /// <exception cref="WorkspaceNotInitializedException">The folder has no .ykanban structure.</exception>
    /// <exception cref="WorkspaceLockException">Another instance holds the lock.</exception>
    public static WorkspaceLock Acquire(string folderPath)
    {
        string lockFilePath = WorkspacePaths.LockFile(folderPath);
        FileStream stream;
        try
        {
            // FileShare.None gives the exclusive semantics; FileMode.Create truncates any stale content.
            stream = new FileStream(lockFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        }
        catch (DirectoryNotFoundException)
        {
            throw new WorkspaceNotInitializedException(folderPath);
        }
        catch (IOException ex)
        {
            throw new WorkspaceLockException(lockFilePath, ReadDiagnosticsBestEffort(lockFilePath), ex);
        }

        // Write holder diagnostics so humans can identify the owning instance from the file alone.
        string diagnostics = $"pid={Environment.ProcessId};machine={Environment.MachineName};time={DateTimeOffset.UtcNow:O}";
        byte[] payload = Encoding.UTF8.GetBytes(diagnostics);
        stream.Write(payload, 0, payload.Length);
        stream.Flush();
        return new WorkspaceLock(stream, lockFilePath);
    }

    /// <summary>Attempts to read holder diagnostics from a lock file we failed to acquire.</summary>
    private static string? ReadDiagnosticsBestEffort(string lockFilePath)
    {
        try
        {
            return File.ReadAllText(lockFilePath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Releases the exclusive handle. The lock file itself is intentionally left on disk.</summary>
    public void Dispose() => _stream.Dispose();
}
