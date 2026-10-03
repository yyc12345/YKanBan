using System.Diagnostics;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;

namespace YKanBan.Services;

/// <summary>
/// Composition root for the whole process: owns the loaded configuration and
/// the single open workspace session. The configuration is persisted once at
/// shutdown, and only when this session changed it; the session (lock plus connection) lives until disposal.
/// </summary>
public sealed class AppServices : IDisposable
{
    /// <summary>
    /// Loads the configuration and prepares the service graph.
    /// </summary>
    public AppServices()
    {
        ConfigStore = new AppConfigStore(AppDataPaths.AppConfigJsonPath);
        Config = ConfigStore.Load();
    }

    /// <summary>
    /// Gets the mutable application configuration, persisted at shutdown.
    /// </summary>
    public AppConfig Config { get; }

    /// <summary>
    /// Gets the store backing app.json.
    /// </summary>
    public AppConfigStore ConfigStore { get; }

    /// <summary>
    /// Gets the open workspace session, or <see langword="null"/> while no
    /// workspace is open (for example on an error page).
    /// </summary>
    public WorkspaceSession? Session { get; private set; }

    /// <summary>
    /// Opens a workspace, acquiring its exclusive lock and connection.
    /// </summary>
    /// <param name="folderPath">The workspace folder to open.</param>
    /// <returns>The open session.</returns>
    /// <exception cref="YKanBan.Storage.WorkspaceNotInitializedException">The folder has no .ykanban structure.</exception>
    /// <exception cref="YKanBan.Storage.WorkspaceLockException">Another instance already holds the lock.</exception>
    public WorkspaceSession OpenWorkspace(string folderPath)
    {
        Session = WorkspaceSession.Open(folderPath);
        return Session;
    }

    /// <summary>
    /// Initializes a workspace with its preset columns and opens it, holding
    /// the lock from the creation of .ykanban onwards.
    /// </summary>
    /// <param name="folderPath">The existing folder to initialize.</param>
    /// <returns>The open session.</returns>
    /// <exception cref="YKanBan.Storage.WorkspaceDirectoryMissingException">The folder does not exist on disk.</exception>
    /// <exception cref="YKanBan.Storage.WorkspaceLockException">Another instance already holds the lock.</exception>
    public WorkspaceSession InitializeWorkspace(string folderPath)
    {
        Session = WorkspaceSession.Initialize(folderPath);
        return Session;
    }

    /// <summary>
    /// Persists the configuration when this session changed a setting; called
    /// once at shutdown. A write failure only loses this session's settings
    /// changes, the same accepted loss as a crash, so it is logged and ignored.
    /// </summary>
    public void SaveConfiguration()
    {
        try
        {
            ConfigStore.SaveIfChanged(Config);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine(exception);
        }
    }

    /// <summary>
    /// Releases the workspace session, if any.
    /// </summary>
    public void Dispose() => Session?.Dispose();
}
