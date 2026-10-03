using Microsoft.Data.Sqlite;

namespace YKanBan.Storage.Workspace;

/// <summary>
/// Creates the .ykanban structure for a folder in the mandated order: the
/// .ykanban folder, then the exclusive lock, then the database. The database
/// schema and the optional preset content are written in one transaction, so a
/// failure never leaves a half-built database behind.
/// </summary>
public static class WorkspaceInitializer
{
    /// <summary>
    /// Returns whether the folder contains a .ykanban structure. Only the
    /// passed folder is checked; parent folders are never searched.
    /// </summary>
    /// <param name="folderPath">The folder to inspect.</param>
    /// <returns><see langword="true"/> when .ykanban exists in the folder.</returns>
    public static bool IsWorkspace(string folderPath) => Directory.Exists(WorkspacePaths.Root(folderPath));

    /// <summary>
    /// Initializes a fresh workspace without any preset content and releases
    /// the lock again.
    /// </summary>
    /// <param name="folderPath">The existing folder to initialize.</param>
    /// <exception cref="WorkspaceDirectoryMissingException">The folder does not exist on disk.</exception>
    /// <exception cref="WorkspaceLockException">Another instance holds the workspace lock.</exception>
    /// <exception cref="InvalidOperationException">The workspace database already exists.</exception>
    public static void Initialize(string folderPath)
    {
        using WorkspaceLock workspaceLock = InitializeLocked(folderPath, addPresetColumns: false);
    }

    /// <summary>
    /// Initializes a fresh workspace and returns the lock still held, so the
    /// caller can open the workspace without ever releasing it.
    /// </summary>
    /// <param name="folderPath">The existing folder to initialize.</param>
    /// <param name="addPresetColumns">Whether to add the preset columns in the schema transaction.</param>
    /// <returns>The held workspace lock.</returns>
    /// <exception cref="WorkspaceDirectoryMissingException">The folder does not exist on disk.</exception>
    /// <exception cref="WorkspaceLockException">Another instance holds the workspace lock.</exception>
    /// <exception cref="InvalidOperationException">The workspace database already exists.</exception>
    internal static WorkspaceLock InitializeLocked(string folderPath, bool addPresetColumns)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new WorkspaceDirectoryMissingException(folderPath);
        }

        // .ykanban first: the lock file lives inside it.
        Directory.CreateDirectory(WorkspacePaths.Root(folderPath));
        WorkspaceLock workspaceLock = WorkspaceLock.Acquire(folderPath);
        try
        {
            using SqliteConnection connection = SqliteDatabase.Create(
                WorkspacePaths.Database(folderPath),
                WorkspaceSchema.Migrations,
                addPresetColumns ? WorkspacePreset.AddPresetColumns : null);
            return workspaceLock;
        }
        catch
        {
            workspaceLock.Dispose();
            throw;
        }
    }
}
