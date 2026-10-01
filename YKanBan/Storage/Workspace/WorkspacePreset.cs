using Microsoft.Data.Sqlite;

namespace YKanBan.Storage.Workspace;

/// <summary>
/// Seeds initial workspace content. The three preset column titles are read
/// straight from the ResX resources in the UI language active at seeding
/// time, then stored as ordinary data that no longer follows language
/// switches.
/// </summary>
public static class WorkspacePreset
{
    /// <summary>Inserts the three preset columns (To Do / In Progress / Done equivalents).</summary>
    /// <exception cref="SqliteException">A title collides with an existing column.</exception>
    public static void SeedColumns(string folderPath)
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

        // Insert all three in one transaction so a half-seeded workspace can never exist.
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
