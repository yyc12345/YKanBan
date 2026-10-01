using Microsoft.Data.Sqlite;

namespace YKanBan.Storage.Workspace;

/// <summary>
/// Creates the .ykanban structure for a folder: the folder itself and an
/// empty, fully migrated database. Seeding preset content is a separate
/// concern handled by <see cref="WorkspacePreset"/>.
/// </summary>
public static class WorkspaceInitializer
{
    /// <summary>Returns true when the folder contains a .ykanban structure (no parent lookup).</summary>
    public static bool IsWorkspace(string folderPath) => Directory.Exists(WorkspacePaths.Root(folderPath));

    /// <summary>Initializes a fresh workspace: creates .ykanban and an empty migrated database.</summary>
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

        // Create the .ykanban folder and the migrated, still empty database.
        Directory.CreateDirectory(WorkspacePaths.Root(folderPath));
        using SqliteConnection connection = SqliteDatabase.Open(databasePath, WorkspaceSchema.Migrations);
    }
}
