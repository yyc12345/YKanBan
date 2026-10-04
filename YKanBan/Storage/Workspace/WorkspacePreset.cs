using Microsoft.Data.Sqlite;

namespace YKanBan.Storage.Workspace;

/// <summary>
/// Adds the initial workspace content. The preset column titles/descriptions and
/// tag names/descriptions are read straight from the ResX resources in the UI
/// language active when they are added; they then become ordinary data that no
/// longer follows language switches.
/// </summary>
public static class WorkspacePreset {
    /// <summary>
    /// Inserts the three preset columns (the current language's equivalents of
    /// To Do / In Progress / Done) with their descriptions in a single transaction.
    /// </summary>
    /// <param name="folderPath">The initialized workspace folder.</param>
    /// <exception cref="SqliteException">The workspace database does not exist or a title collides with an existing column.</exception>
    public static void AddPresetColumns(string folderPath) {
        using SqliteConnection connection = SqliteDatabase.Open(
            WorkspacePaths.Database(folderPath), WorkspaceSchema.Migrations);

        long now = UnixTime.Now;

        // Preset titles and descriptions are data at creation time: whatever the current language says gets stored.
        (string Title, string Description)[] columns =
        [
            (Resources.PresetColumn_Todo, Resources.PresetColumn_Todo_Description),
            (Resources.PresetColumn_InProgress, Resources.PresetColumn_InProgress_Description),
            (Resources.PresetColumn_Done, Resources.PresetColumn_Done_Description),
        ];

        // Insert all three atomically so a partially populated workspace can never exist.
        using var transaction = connection.BeginTransaction();
        foreach ((string title, string description) in columns) {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO columns (title, description, created_at, updated_at)
                VALUES ($title, $description, $now, $now);
                """;
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$description", description);
            command.Parameters.AddWithValue("$now", now);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    /// <summary>
    /// Inserts the preset tag set (names, colors and descriptions in the current
    /// language) in a single transaction.
    /// </summary>
    /// <param name="folderPath">The initialized workspace folder.</param>
    /// <exception cref="SqliteException">The workspace database does not exist or a tag name collides.</exception>
    public static void AddPresetTags(string folderPath) {
        using SqliteConnection connection = SqliteDatabase.Open(
            WorkspacePaths.Database(folderPath), WorkspaceSchema.Migrations);

        // Colors are stored in the canonical lowercase #rrggbb form.
        (string Name, string Color, string Description)[] tags =
        [
            (Resources.PresetTag_Bug_Name, "#d73a4a", Resources.PresetTag_Bug_Description),
            (Resources.PresetTag_Documentation_Name, "#0075ca", Resources.PresetTag_Documentation_Description),
            (Resources.PresetTag_Duplicate_Name, "#cfd3d7", Resources.PresetTag_Duplicate_Description),
            (Resources.PresetTag_Enhancement_Name, "#a2eeef", Resources.PresetTag_Enhancement_Description),
            (Resources.PresetTag_HelpWanted_Name, "#008672", Resources.PresetTag_HelpWanted_Description),
            (Resources.PresetTag_Invalid_Name, "#e4e669", Resources.PresetTag_Invalid_Description),
            (Resources.PresetTag_Question_Name, "#d876e3", Resources.PresetTag_Question_Description),
            (Resources.PresetTag_Wontfix_Name, "#ffffff", Resources.PresetTag_Wontfix_Description),
        ];

        using var transaction = connection.BeginTransaction();
        foreach ((string name, string color, string description) in tags) {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO tags (name, color, description)
                VALUES ($name, $color, $description);
                """;
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$color", color);
            command.Parameters.AddWithValue("$description", description);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
