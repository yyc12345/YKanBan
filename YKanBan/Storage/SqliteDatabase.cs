using Microsoft.Data.Sqlite;

namespace YKanBan.Storage;

/// <summary>
/// Shared SQLite plumbing for workspace databases: opens a connection with
/// the mandated PRAGMA configuration (WAL journal, NORMAL synchronous mode,
/// foreign keys on), registers the custom SQL functions and applies
/// incremental <c>user_version</c> migrations.
/// </summary>
public static class SqliteDatabase
{
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
    public static SqliteConnection Open(string databasePath, IReadOnlyList<SchemaMigration> migrations)
    {
        SqliteConnection connection = OpenConnection(databasePath);
        try
        {
            ApplyPragmas(connection);
            RegisterFunctions(connection);
            ApplyMigrations(connection, migrations);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a new database file with the mandatory PRAGMAs, then applies
    /// every migration and the optional seed in one transaction, so the file
    /// ends up either fully built and seeded or still at version 0.
    /// </summary>
    /// <param name="databasePath">Path of the SQLite database file; it must not exist yet.</param>
    /// <param name="migrations">Contiguous migration list ordered by version, starting at 1.</param>
    /// <param name="seed">Initial content written in the same transaction, when any.</param>
    /// <returns>An open connection with all PRAGMAs applied and the schema migrated.</returns>
    /// <exception cref="ArgumentException">The migration list is not contiguous from version 1.</exception>
    /// <exception cref="InvalidOperationException">The database file already exists.</exception>
    public static SqliteConnection Create(
        string databasePath,
        IReadOnlyList<SchemaMigration> migrations,
        Action<SqliteConnection, SqliteTransaction>? seed = null)
    {
        ValidateMigrations(migrations);
        if (File.Exists(databasePath))
        {
            throw new InvalidOperationException($"The database already exists: {databasePath}");
        }

        SqliteConnection connection = OpenConnection(databasePath);
        try
        {
            ApplyPragmas(connection);
            RegisterFunctions(connection);

            using var transaction = connection.BeginTransaction();
            foreach (SchemaMigration migration in migrations)
            {
                Execute(connection, migration.Sql, transaction);
                Execute(connection, $"PRAGMA user_version={migration.Version};", transaction);
            }
            seed?.Invoke(connection, transaction);
            transaction.Commit();
            return connection;
        }
        catch
        {
            connection.Dispose();

            // Leave no empty version-0 file behind that a later open would migrate into an empty board.
            DeleteDatabaseFilesBestEffort(databasePath);
            throw;
        }
    }

    /// <summary>
    /// Deletes a database file together with its WAL and shared-memory files,
    /// ignoring failures.
    /// </summary>
    /// <param name="databasePath">Path of the SQLite database file.</param>
    private static void DeleteDatabaseFilesBestEffort(string databasePath)
    {
        foreach (string path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm", databasePath + "-journal" })
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Best effort: the original failure is what gets reported.
            }
        }
    }

    /// <summary>
    /// Reads the stored <c>PRAGMA user_version</c> value.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <returns>The stored schema version.</returns>
    public static long ReadUserVersion(SqliteConnection connection)
    {
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
    internal static void ApplyPragmas(SqliteConnection connection)
    {
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
    internal static void RegisterFunctions(SqliteConnection connection)
    {
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
    internal static void ApplyMigrations(SqliteConnection connection, IReadOnlyList<SchemaMigration> migrations)
    {
        ValidateMigrations(migrations);

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

    /// <summary>
    /// Opens a connection to the file, creating it if needed, without any configuration.
    /// </summary>
    /// <param name="databasePath">Path of the SQLite database file.</param>
    /// <returns>The open connection.</returns>
    private static SqliteConnection OpenConnection(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,

            // Each process holds its own single connection; a pool would only add noise.
            Pooling = false,
        }.ToString());

        connection.Open();
        return connection;
    }

    /// <summary>
    /// Checks that the migration list is ordered, contiguous and starts at version 1.
    /// </summary>
    /// <param name="migrations">The migration list.</param>
    /// <exception cref="ArgumentException">The list breaks the contract.</exception>
    private static void ValidateMigrations(IReadOnlyList<SchemaMigration> migrations)
    {
        for (int index = 0; index < migrations.Count; index++)
        {
            if (migrations[index].Version != index + 1)
            {
                throw new ArgumentException("Migrations must be contiguous and start at version 1.", nameof(migrations));
            }
        }
    }

    /// <summary>
    /// Executes a non-query SQL script, optionally inside an explicit transaction.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <param name="commandText">The SQL script to execute.</param>
    /// <param name="transaction">The transaction to enlist in, when any.</param>
    private static void Execute(SqliteConnection connection, string commandText, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}
