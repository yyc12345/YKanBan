using Microsoft.Data.Sqlite;

namespace YKanBan.Storage.AppData;

/// <summary>A known workspace row of the global app.db.</summary>
/// <param name="Path">Workspace folder path as registered.</param>
/// <param name="LastEditedAt">Unix seconds of the last successful data write to the workspace database.</param>
/// <param name="AddedAt">Unix seconds when the workspace was registered.</param>
public sealed record WorkspaceRecord(string Path, long LastEditedAt, long AddedAt);

/// <summary>
/// Global application database (app.db): stores the persistent workspace
/// list. Any successful data write into a workspace database must be
/// mirrored here by refreshing last_edited_at (the write-through is wired
/// by the repository layer). Uses the same PRAGMA configuration as the
/// workspace databases.
/// </summary>
public sealed class AppDatabase : IDisposable
{
    /// <summary>Latest app.db schema version this build understands.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly IReadOnlyList<SchemaMigration> Migrations = new SchemaMigration[]
    {
        new SchemaMigration(CurrentSchemaVersion, "initial schema", """
            CREATE TABLE workspaces (
                path           TEXT    PRIMARY KEY,
                last_edited_at INTEGER NOT NULL,
                added_at       INTEGER NOT NULL
            );
            """),
    };

    private readonly SqliteConnection _connection;
    private readonly Func<long> _unixNow;
    private readonly object _gate = new();

    /// <summary>Opens (creating if needed) the app.db file and applies pending migrations.</summary>
    /// <param name="databasePath">Path of the app.db file.</param>
    /// <param name="unixNow">Clock override for tests; defaults to real Unix time.</param>
    public AppDatabase(string databasePath, Func<long>? unixNow = null)
    {
        // Ensure the application data folder exists before creating the database file.
        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _unixNow = unixNow ?? (() => UnixTime.Now);
        _connection = SqliteDatabase.Open(databasePath, Migrations);
    }

    /// <summary>Returns all known workspaces ordered by added_at ascending.</summary>
    public IReadOnlyList<WorkspaceRecord> GetWorkspaces()
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT path, last_edited_at, added_at FROM workspaces ORDER BY added_at ASC, path ASC;";
            using var reader = command.ExecuteReader();

            var records = new List<WorkspaceRecord>();
            while (reader.Read())
            {
                records.Add(new WorkspaceRecord(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2)));
            }
            return records;
        }
    }

    /// <summary>Registers a workspace, or refreshes last_edited_at if it is already known.</summary>
    public void UpsertWorkspace(string path)
    {
        lock (_gate)
        {
            long now = _unixNow();
            using var command = _connection.CreateCommand();
            // On conflict only last_edited_at moves; the original added_at is preserved.
            command.CommandText = """
                INSERT INTO workspaces (path, last_edited_at, added_at)
                VALUES ($path, $now, $now)
                ON CONFLICT(path) DO UPDATE SET last_edited_at = $now;
                """;
            command.Parameters.AddWithValue("$path", path);
            command.Parameters.AddWithValue("$now", now);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Refreshes last_edited_at for a known workspace; registers it if the row is absent.</summary>
    public void TouchLastEdited(string path)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "UPDATE workspaces SET last_edited_at = $now WHERE path = $path;";
            command.Parameters.AddWithValue("$path", path);
            command.Parameters.AddWithValue("$now", _unixNow());
            if (command.ExecuteNonQuery() == 0)
            {
                // Self-healing path: a workspace opened before registration still gets a row.
                UpsertWorkspace(path);
            }
        }
    }

    /// <summary>Forgets a workspace entry; the .ykanban folder itself is untouched.</summary>
    public void RemoveWorkspace(string path)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM workspaces WHERE path = $path;";
            command.Parameters.AddWithValue("$path", path);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Closes the underlying connection.</summary>
    public void Dispose() => _connection.Dispose();
}
