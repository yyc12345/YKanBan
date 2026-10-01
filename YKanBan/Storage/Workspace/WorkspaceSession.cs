namespace YKanBan.Storage.Workspace;

/// <summary>One open workspace: the exclusive lock plus its repository.</summary>
public sealed class WorkspaceSession : IDisposable
{
    /// <summary>Canonical workspace folder path.</summary>
    public required string FolderPath { get; init; }

    /// <summary>The exclusive lock held while this session is open.</summary>
    public required WorkspaceLock Lock { get; init; }

    /// <summary>The data access object for this workspace.</summary>
    public required WorkspaceRepository Repository { get; init; }

    /// <summary>Disposes the repository, then releases the lock.</summary>
    public void Dispose()
    {
        Repository.Dispose();
        Lock.Dispose();
    }
}
