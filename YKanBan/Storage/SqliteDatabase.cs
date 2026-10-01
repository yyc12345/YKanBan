using Microsoft.Data.Sqlite;

namespace YKanBan.Storage;

/// <summary>
/// Shared SQLite plumbing for both the per-workspace databases and the
/// global app.db: opens connections with the mandated PRAGMA configuration
/// (WAL journal, NORMAL synchronous mode, foreign keys on) and applies
/// incremental user_version migrations.
/// </summary>
public static class SqliteDatabase
{
    /// <summary>Opens (creating if needed) a database file, applies PRAGMAs and pending migrations.</summary>
    /// <param name="databasePath">Path of the SQLite database file.</param>
    /// <param name="migrations">Contiguous migration list, ordered by version starting at 1.</param>
    /// <returns>An open connection with all PRAGMAs applied and the schema migrated.</returns>
    public static SqliteConnection Open(string databasePath, IReadOnlyList<SchemaMigration> migrations)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        try
        {
            ApplyPragmas(connection);
            ApplyMigrations(connection, migrations);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>Reads the stored PRAGMA user_version value.</summary>
    public static long ReadUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>Applies per-connection PRAGMAs. WAL itself is a persistent database property.</summary>
    internal static void ApplyPragmas(SqliteConnection connection)
    {
        // WAL plus NORMAL synchronous is the chosen durability/performance trade-off;
        // foreign_keys must be enabled on every connection or DDL-level cascades never fire.
        Execute(connection, "PRAGMA journal_mode=WAL;");
        Execute(connection, "PRAGMA synchronous=NORMAL;");
        Execute(connection, "PRAGMA foreign_keys=ON;");
    }

    /// <summary>Runs every migration newer than the stored user_version, each in its own transaction.</summary>
    /// <exception cref="SchemaVersionException">The database was written by a newer build.</exception>
    /// <exception cref="ArgumentException">The migration list is not contiguous from version 1.</exception>
    internal static void ApplyMigrations(SqliteConnection connection, IReadOnlyList<SchemaMigration> migrations)
    {
        // Contract check: the list must be contiguous and ordered, starting at version 1.
        for (int index = 0; index < migrations.Count; index++)
        {
            if (migrations[index].Version != index + 1)
            {
                throw new ArgumentException("Migrations must be contiguous and start at version 1.", nameof(migrations));
            }
        }

        long current = ReadUserVersion(connection);
        int latest = migrations.Count;

        // A database written by a newer build cannot be managed by this one; refuse loudly.
        if (current > latest)
        {
            throw new SchemaVersionException(current, latest);
        }

        // Apply each pending migration atomically so a crash never leaves a half-migrated database.
        foreach (SchemaMigration migration in migrations)
        {
            if (migration.Version <= current)
            {
                continue;
            }
            using var transaction = connection.BeginTransaction();
            Execute(connection, migration.Sql, transaction);
            Execute(connection, $"PRAGMA user_version={migration.Version};", transaction);
            transaction.Commit();
        }
    }

    /// <summary>Executes a non-query SQL script, optionally inside an explicit transaction.</summary>
    private static void Execute(SqliteConnection connection, string commandText, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}
