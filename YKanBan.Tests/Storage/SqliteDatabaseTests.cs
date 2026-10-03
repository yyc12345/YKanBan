using Microsoft.Data.Sqlite;
using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage;

/// <summary>
/// Tests for the shared SQLite plumbing: mandatory PRAGMAs and the
/// user_version-driven migration runner.
/// </summary>
[TestClass]
public class SqliteDatabaseTests
{
    [TestMethod]
    public void OpenAppliesMandatoryPragmas()
    {
        using var directory = new TempDirectory();
        string databasePath = Path.Combine(directory.FullPath, "pragmas.db");

        using SqliteConnection connection = SqliteDatabase.Open(databasePath, []);
        Assert.AreEqual("wal", SqliteTestHelper.ScalarString(connection, "PRAGMA journal_mode;"));
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(connection, "PRAGMA synchronous;"));
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(connection, "PRAGMA foreign_keys;"));
    }

    [TestMethod]
    public void OpenSetsUserVersionToLatestMigration()
    {
        using var directory = new TempDirectory();
        string databasePath = Path.Combine(directory.FullPath, "version.db");

        using SqliteConnection connection = SqliteDatabase.Open(databasePath, WorkspaceSchema.Migrations);
        Assert.AreEqual(WorkspaceSchema.CurrentVersion, SqliteDatabase.ReadUserVersion(connection));
    }

    [TestMethod]
    public void ReopenKeepsWalJournalMode()
    {
        using var directory = new TempDirectory();
        string databasePath = Path.Combine(directory.FullPath, "wal.db");

        using (SqliteConnection connection = SqliteDatabase.Open(databasePath, WorkspaceSchema.Migrations))
        {
        }

        // WAL mode is stored in the database header, so a fresh connection keeps it.
        using SqliteConnection reopened = SqliteDatabase.Open(databasePath, WorkspaceSchema.Migrations);
        Assert.AreEqual("wal", SqliteTestHelper.ScalarString(reopened, "PRAGMA journal_mode;"));
    }

    [TestMethod]
    public void OpenRejectsNewerStoredSchemaVersion()
    {
        using var directory = new TempDirectory();
        string databasePath = Path.Combine(directory.FullPath, "newer.db");

        long newerVersion = WorkspaceSchema.CurrentVersion + 1;
        using (SqliteConnection connection = SqliteDatabase.Open(databasePath, WorkspaceSchema.Migrations))
        {
            SqliteTestHelper.Exec(connection, $"PRAGMA user_version={newerVersion};");
        }

        SchemaVersionException exception = Assert.ThrowsExactly<SchemaVersionException>(
            () => SqliteDatabase.Open(databasePath, WorkspaceSchema.Migrations));
        Assert.AreEqual(newerVersion, exception.StoredVersion);
        Assert.AreEqual(WorkspaceSchema.CurrentVersion, exception.SupportedVersion);
    }

    [TestMethod]
    public void OpenRejectsNonContiguousMigrationList()
    {
        using var directory = new TempDirectory();
        string databasePath = Path.Combine(directory.FullPath, "gap.db");

        SchemaMigration[] broken =
        [
            new SchemaMigration(2, "starts too high", "SELECT 1;"),
        ];

        Assert.ThrowsExactly<ArgumentException>(() => SqliteDatabase.Open(databasePath, broken));
    }

    [TestMethod]
    public void CreateBuildsSchemaAndSeedTogether()
    {
        using var directory = new TempDirectory();
        string databasePath = Path.Combine(directory.FullPath, "create.db");

        using SqliteConnection connection = SqliteDatabase.Create(databasePath, WorkspaceSchema.Migrations,
            (seedConnection, transaction) =>
            {
                using SqliteCommand command = seedConnection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO tags (name, color) VALUES ('seed', '#000000');";
                command.ExecuteNonQuery();
            });
        Assert.AreEqual(WorkspaceSchema.CurrentVersion, SqliteDatabase.ReadUserVersion(connection));
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM tags;"));
        Assert.AreEqual("wal", SqliteTestHelper.ScalarString(connection, "PRAGMA journal_mode;"));
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(connection, "PRAGMA foreign_keys;"));
    }

    [TestMethod]
    public void CreateWithFailingSeedLeavesNoDatabaseBehind()
    {
        using var directory = new TempDirectory();
        string databasePath = Path.Combine(directory.FullPath, "failed.db");

        Assert.ThrowsExactly<InvalidOperationException>(() => SqliteDatabase.Create(
            databasePath, WorkspaceSchema.Migrations, (_, _) => throw new InvalidOperationException("seed failed")));
        Assert.IsFalse(File.Exists(databasePath));
        Assert.IsFalse(File.Exists(databasePath + "-wal"));
        Assert.IsFalse(File.Exists(databasePath + "-shm"));
    }

    [TestMethod]
    public void CreateRefusesAnExistingDatabaseAndKeepsIt()
    {
        using var directory = new TempDirectory();
        string databasePath = Path.Combine(directory.FullPath, "existing.db");
        SqliteDatabase.Create(databasePath, WorkspaceSchema.Migrations).Dispose();

        Assert.ThrowsExactly<InvalidOperationException>(() => SqliteDatabase.Create(databasePath, WorkspaceSchema.Migrations));
        Assert.IsTrue(File.Exists(databasePath));
    }
}
