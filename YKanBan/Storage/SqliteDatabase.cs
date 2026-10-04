using Microsoft.Data.Sqlite;

namespace YKanBan.Storage;

/// <summary>
/// Shared SQLite plumbing for workspace databases: opens a connection with
/// the mandated PRAGMA configuration (WAL journal, NORMAL synchronous mode,
/// foreign keys on) and applies incremental <c>user_version</c> migrations.
/// </summary>
public static class SqliteDatabase {
    /// <summary>
    /// Name of the per-connection SQL function that applies <see cref="Fold"/>;
    /// search uses it for Unicode case-insensitive matching.
    /// </summary>
    public const string FoldFunctionName = "ykb_fold";

    /// <summary>
    /// Unicode case folding shared by the SQL function and the search
    /// parameters it is compared against; comparisons after folding are ordinal.
    /// </summary>
    /// <param name="text">The text to fold.</param>
    /// <returns>The invariant lowercase form.</returns>
    public static string Fold(string text) => text.ToLowerInvariant();

    /// <summary>
    /// Opens (creating if needed) a database file, applies the mandatory
    /// PRAGMAs and runs any pending migrations.
    /// </summary>
    /// <param name="databasePath">Path of the SQLite database file.</param>
    /// <param name="migrations">Contiguous migration list ordered by version, starting at 1.</param>
    /// <returns>An open connection with all PRAGMAs applied and the schema migrated.</returns>
    public static SqliteConnection Open(string databasePath, IReadOnlyList<SchemaMigration> migrations) {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,

            // Each process holds its own single connection; a pool would only add noise.
            Pooling = false,
        }.ToString());

        connection.Open();
        try {
            ApplyPragmas(connection);
            RegisterFunctions(connection);
            ApplyMigrations(connection, migrations);
            return connection;
        } catch {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads the stored <c>PRAGMA user_version</c> value.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <returns>The stored schema version.</returns>
    public static long ReadUserVersion(SqliteConnection connection) {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Applies the per-connection PRAGMAs. WAL is a persistent database
    /// property, but the statement is harmless to repeat; foreign keys must be
    /// enabled on every connection or the DDL cascades never fire.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    internal static void ApplyPragmas(SqliteConnection connection) {
        // WAL plus NORMAL synchronous is the chosen durability/performance trade-off.
        Execute(connection, "PRAGMA journal_mode=WAL;");
        Execute(connection, "PRAGMA synchronous=NORMAL;");
        Execute(connection, "PRAGMA foreign_keys=ON;");
    }

    /// <summary>
    /// Registers the custom SQL functions; like the PRAGMAs they live on the
    /// connection and must be registered on every one.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    internal static void RegisterFunctions(SqliteConnection connection) {
        // NULL never reaches it from the NOT NULL text columns, but stays NULL-safe regardless.
        connection.CreateFunction<string?, string?>(
            FoldFunctionName, text => text is null ? null : Fold(text), isDeterministic: true);
    }

    /// <summary>
    /// Runs every migration newer than the stored <c>user_version</c>, each in
    /// its own transaction together with its version bump.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <param name="migrations">Contiguous migration list ordered by version, starting at 1.</param>
    /// <exception cref="ArgumentException">The migration list is not contiguous from version 1.</exception>
    /// <exception cref="SchemaVersionException">The database was written by a newer build.</exception>
    internal static void ApplyMigrations(SqliteConnection connection, IReadOnlyList<SchemaMigration> migrations) {
        // Contract check: the list must be ordered, contiguous and start at version 1.
        for (int index = 0; index < migrations.Count; index++) {
            if (migrations[index].Version != index + 1) {
                throw new ArgumentException("Migrations must be contiguous and start at version 1.", nameof(migrations));
            }
        }

        long current = ReadUserVersion(connection);
        int latest = migrations.Count;

        // A database written by a newer build cannot be managed by this one; refuse loudly.
        if (current > latest) {
            throw new SchemaVersionException(current, latest);
        }

        // Apply each pending migration atomically so a crash never leaves a half-migrated database.
        foreach (SchemaMigration migration in migrations) {
            if (migration.Version <= current) {
                continue;
            }

            using var transaction = connection.BeginTransaction();
            Execute(connection, migration.Sql, transaction);
            Execute(connection, $"PRAGMA user_version={migration.Version};", transaction);
            transaction.Commit();
        }
    }

    /// <summary>
    /// Executes a non-query SQL script, optionally inside an explicit transaction.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <param name="commandText">The SQL script to execute.</param>
    /// <param name="transaction">The transaction to enlist in, when any.</param>
    private static void Execute(SqliteConnection connection, string commandText, SqliteTransaction? transaction = null) {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}
