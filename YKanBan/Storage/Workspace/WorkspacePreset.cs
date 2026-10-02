using Microsoft.Data.Sqlite;

namespace YKanBan.Storage.Workspace;

/// <summary>
/// Adds the initial workspace content. The three preset column titles are read
/// straight from the ResX resources in the UI language active when they are
/// added; they then become ordinary data that no longer follows language
/// switches.
/// </summary>
public static class WorkspacePreset
{
    /// <summary>
    /// Inserts the three preset columns (the current language's equivalents of
    /// To Do / In Progress / Done) in a single transaction.
    /// </summary>
    /// <param name="folderPath">The initialized workspace folder.</param>
    /// <exception cref="SqliteException">The workspace database does not exist or a title collides with an existing column.</exception>
    public static void AddPresetColumns(string folderPath)
    {
        using SqliteConnection connection = SqliteDatabase.Open(
            WorkspacePaths.Database(folderPath), WorkspaceSchema.Migrations);

        long now = UnixTime.Now;

        // Preset titles are data at creation time: whatever the current language says gets stored.
        string[] titles =
        [
            Resources.PresetColumn_Todo,
            Resources.PresetColumn_InProgress,
            Resources.PresetColumn_Done,
        ];

        // Insert all three atomically so a partially populated workspace can never exist.
        using var transaction = connection.BeginTransaction();
        foreach (string title in titles)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO columns (title, description, created_at, updated_at)
                VALUES ($title, '', $now, $now);
                """;
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$now", now);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
