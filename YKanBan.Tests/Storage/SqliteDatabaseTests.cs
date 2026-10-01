using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage;

/// <summary>Tests for PRAGMA application and user_version migration machinery.</summary>
[TestClass]
public class SqliteDatabaseTests
{
    private static readonly SchemaMigration[] V1Only =
    [
        new SchemaMigration(1, "v1", "CREATE TABLE t1 (id INTEGER PRIMARY KEY);"),
    ];

    private static readonly SchemaMigration[] V1AndV2 =
    [
        new SchemaMigration(1, "v1", "CREATE TABLE t1 (id INTEGER PRIMARY KEY);"),
        new SchemaMigration(2, "v2", "CREATE TABLE t2 (id INTEGER PRIMARY KEY);"),
    ];

    [TestMethod]
    public void OpenAppliesMandatedPragmas()
    {
        using var directory = new TempDirectory();
        string dbPath = Path.Combine(directory.FullPath, "test.db");

        using var connection = SqliteDatabase.Open(dbPath, []);
        Assert.AreEqual("wal", SqliteTestHelper.ScalarString(connection, "PRAGMA journal_mode;"));
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(connection, "PRAGMA synchronous;"));
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(connection, "PRAGMA foreign_keys;"));
    }

    [TestMethod]
    public void OpenSetsUserVersionToLatestWorkspaceMigration()
    {
        using var directory = new TempDirectory();
        string dbPath = Path.Combine(directory.FullPath, "test.db");

        using var connection = SqliteDatabase.Open(dbPath, WorkspaceSchema.Migrations);
        Assert.AreEqual(WorkspaceSchema.CurrentVersion, SqliteDatabase.ReadUserVersion(connection));
    }

    [TestMethod]
    public void MigrationsApplyIncrementallyOnReopen()
    {
        using var directory = new TempDirectory();
        string dbPath = Path.Combine(directory.FullPath, "test.db");

        // First open with only v1 available: v1 applied, v2 table absent.
        using (var connection = SqliteDatabase.Open(dbPath, V1Only))
        {
            Assert.AreEqual(1L, SqliteDatabase.ReadUserVersion(connection));
            Assert.IsTrue(SqliteTestHelper.TableExists(connection, "t1"));
            Assert.IsFalse(SqliteTestHelper.TableExists(connection, "t2"));
        }

        // Reopen with v1+v2: only v2 runs, v1 data untouched.
        using (var connection = SqliteDatabase.Open(dbPath, V1AndV2))
        {
            Assert.AreEqual(2L, SqliteDatabase.ReadUserVersion(connection));
            Assert.IsTrue(SqliteTestHelper.TableExists(connection, "t1"));
            Assert.IsTrue(SqliteTestHelper.TableExists(connection, "t2"));
        }
    }

    [TestMethod]
    public void ReopenWithCurrentSchemaIsIdempotent()
    {
        using var directory = new TempDirectory();
        string dbPath = Path.Combine(directory.FullPath, "test.db");

        SqliteDatabase.Open(dbPath, V1AndV2).Dispose();
        using (var connection = SqliteDatabase.Open(dbPath, V1AndV2))
        {
            // Still at version 2; re-running is a no-op.
            Assert.AreEqual(2L, SqliteDatabase.ReadUserVersion(connection));
            SqliteTestHelper.Exec(connection, "INSERT INTO t1 (id) VALUES (42);");
        }

        using (var connection = SqliteDatabase.Open(dbPath, V1AndV2))
        {
            Assert.AreEqual(42L, SqliteTestHelper.ScalarLong(connection, "SELECT id FROM t1;"));
        }
    }

    [TestMethod]
    public void NewerSchemaVersionIsRejected()
    {
        using var directory = new TempDirectory();
        string dbPath = Path.Combine(directory.FullPath, "test.db");

        // Simulate a database written by a newer build.
        using (var connection = SqliteDatabase.Open(dbPath, []))
        {
            SqliteTestHelper.Exec(connection, "PRAGMA user_version=5;");
        }

        SchemaVersionException ex = Assert.ThrowsExactly<SchemaVersionException>(
            () => SqliteDatabase.Open(dbPath, V1AndV2));
        Assert.AreEqual(5L, ex.StoredVersion);
        Assert.AreEqual(2, ex.SupportedVersion);
    }

    [TestMethod]
    public void NonContiguousMigrationListIsRejected()
    {
        using var directory = new TempDirectory();
        string dbPath = Path.Combine(directory.FullPath, "test.db");
        var bad = new[] { new SchemaMigration(2, "gap", "CREATE TABLE t (id INTEGER);") };

        Assert.ThrowsExactly<ArgumentException>(() => SqliteDatabase.Open(dbPath, bad));
    }
}
