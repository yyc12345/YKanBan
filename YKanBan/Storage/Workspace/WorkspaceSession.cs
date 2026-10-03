namespace YKanBan.Storage.Workspace;

/// <summary>
/// One open workspace for the process lifetime: the exclusive lock plus the
/// single repository connection. Opening acquires the lock first, so a folder
/// without .ykanban, or one already locked by another instance, fails before
/// any database work happens. Disposing releases the repository and then the
/// lock.
/// </summary>
public sealed class WorkspaceSession : IDisposable
{
    private readonly WorkspaceLock _workspaceLock;

    /// <summary>
    /// Initializes the session around an acquired lock and an open repository.
    /// </summary>
    /// <param name="folderPath">The canonical workspace folder path.</param>
    /// <param name="workspaceLock">The exclusive lock held while this session is open.</param>
    /// <param name="repository">The data access object for this workspace.</param>
    private WorkspaceSession(string folderPath, WorkspaceLock workspaceLock, WorkspaceRepository repository)
    {
        FolderPath = folderPath;
        _workspaceLock = workspaceLock;
        Repository = repository;
    }

    /// <summary>
    /// Gets the canonical workspace folder path.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    /// Gets the data access object for this workspace.
    /// </summary>
    public WorkspaceRepository Repository { get; }

    /// <summary>
    /// Opens a workspace: acquires its exclusive lock and opens its repository.
    /// </summary>
    /// <param name="folderPath">The workspace folder to open.</param>
    /// <returns>The opened session; dispose it to release the lock and connection.</returns>
    /// <exception cref="WorkspaceNotInitializedException">The folder has no .ykanban structure.</exception>
    /// <exception cref="WorkspaceLockException">Another instance already holds the lock.</exception>
    public static WorkspaceSession Open(string folderPath) => OpenLocked(folderPath, WorkspaceLock.Acquire(folderPath));

    /// <summary>
    /// Initializes a workspace with its preset columns and opens it. The lock
    /// is taken right after .ykanban is created and is never released in
    /// between, so no other instance can interleave with the initialization.
    /// </summary>
    /// <param name="folderPath">The existing folder to initialize.</param>
    /// <returns>The opened session; dispose it to release the lock and connection.</returns>
    /// <exception cref="WorkspaceDirectoryMissingException">The folder does not exist on disk.</exception>
    /// <exception cref="WorkspaceLockException">Another instance holds the workspace lock.</exception>
    /// <exception cref="InvalidOperationException">The workspace database already exists.</exception>
    public static WorkspaceSession Initialize(string folderPath) =>
        OpenLocked(folderPath, WorkspaceInitializer.InitializeLocked(folderPath, addPresetColumns: true));

    /// <summary>
    /// Opens the repository under an already held lock; the lock is released if that fails.
    /// </summary>
    /// <param name="folderPath">The workspace folder.</param>
    /// <param name="workspaceLock">The held workspace lock.</param>
    /// <returns>The opened session.</returns>
    private static WorkspaceSession OpenLocked(string folderPath, WorkspaceLock workspaceLock)
    {
        try
        {
            var repository = new WorkspaceRepository(folderPath);
            return new WorkspaceSession(folderPath, workspaceLock, repository);
        }
        catch
        {
            workspaceLock.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Releases the repository connection, then the exclusive lock.
    /// </summary>
    public void Dispose()
    {
        Repository.Dispose();
        _workspaceLock.Dispose();
    }
}
