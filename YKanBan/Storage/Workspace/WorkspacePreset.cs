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
    /// To Do / In Progress / Done) inside the caller's transaction, so the
    /// presets land together with the schema or not at all.
    /// </summary>
    /// <param name="connection">The connection to the freshly created database.</param>
    /// <param name="transaction">The transaction that also builds the schema.</param>
    /// <exception cref="SqliteException">A title collides with an existing column.</exception>
    public static void AddPresetColumns(SqliteConnection connection, SqliteTransaction transaction)
    {
        long now = UnixTime.Now;

        // Preset titles are data at creation time: whatever the current language says gets stored.
        string[] titles =
        [
            Resources.PresetColumn_Todo,
            Resources.PresetColumn_InProgress,
            Resources.PresetColumn_Done,
        ];

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
    }
}
