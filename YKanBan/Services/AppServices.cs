using YKanBan.Platform;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;

namespace YKanBan.Services;

/// <summary>
/// Composition root: owns the application-wide configuration, the global
/// workspace registry and the workspace connection pool for the whole
/// process lifetime. The configuration is persisted once at shutdown.
/// </summary>
public sealed class AppServices : IDisposable
{
    /// <summary>Maximum number of simultaneously open workspaces.</summary>
    private const int PoolCapacity = 8;

    /// <summary>Loads the configuration and opens the global services.</summary>
    public AppServices()
    {
        ConfigStore = new AppConfigStore(AppDataPaths.AppConfigJsonPath);
        Config = ConfigStore.Load();
        Database = new AppDatabase(AppDataPaths.AppDatabasePath);
        PathComparison = PathComparer.ForCurrentPlatform();
        Pool = new WorkspaceConnectionPool(PoolCapacity, Database, PathComparison);
    }

    /// <summary>The mutable application configuration; persisted by <see cref="SaveConfiguration"/>.</summary>
    public AppConfig Config { get; }

    /// <summary>Store for app.json.</summary>
    public AppConfigStore ConfigStore { get; }

    /// <summary>Global workspace registry (app.db).</summary>
    public AppDatabase Database { get; }

    /// <summary>LRU pool of open workspace sessions.</summary>
    public WorkspaceConnectionPool Pool { get; }

    /// <summary>Platform path comparison rules.</summary>
    public PathComparer PathComparison { get; }

    /// <summary>Persists the configuration; called once at shutdown.</summary>
    public void SaveConfiguration() => ConfigStore.Save(Config);

    /// <summary>Releases the pool and the registry.</summary>
    public void Dispose()
    {
        Pool.Dispose();
        Database.Dispose();
    }
}
