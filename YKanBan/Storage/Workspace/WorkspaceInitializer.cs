using Microsoft.Data.Sqlite;

namespace YKanBan.Storage.Workspace;

/// <summary>
/// Creates the .ykanban structure for a folder: the folder itself and an
/// empty, fully migrated database. Adding preset content is deliberately a
/// separate concern handled by <see cref="WorkspacePreset"/>, so the
/// database can be created without any rows.
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
    /// Initializes a fresh workspace: creates .ykanban and an empty migrated
    /// database, without adding any preset content.
    /// </summary>
    /// <param name="folderPath">The existing folder to initialize.</param>
    /// <exception cref="WorkspaceDirectoryMissingException">The folder does not exist on disk.</exception>
    /// <exception cref="InvalidOperationException">The workspace database already exists.</exception>
    public static void Initialize(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new WorkspaceDirectoryMissingException(folderPath);
        }

        string databasePath = WorkspacePaths.Database(folderPath);
        if (File.Exists(databasePath))
        {
            throw new InvalidOperationException($"The workspace database already exists: {databasePath}");
        }

        // Create the .ykanban folder, then the migrated but still empty database.
        Directory.CreateDirectory(WorkspacePaths.Root(folderPath));
        using SqliteConnection connection = SqliteDatabase.Open(databasePath, WorkspaceSchema.Migrations);
    }
}
