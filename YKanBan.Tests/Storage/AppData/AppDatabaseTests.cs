using Microsoft.Data.Sqlite;
using YKanBan.Storage;
using YKanBan.Storage.AppData;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.AppData;

/// <summary>Tests for the global app.db workspace registry.</summary>
[TestClass]
public class AppDatabaseTests
{
    private static string DatabasePath(TempDirectory directory)
        => Path.Combine(directory.FullPath, "app.db");

    [TestMethod]
    public void UpsertInsertsRowWithTimestamps()
    {
        using var directory = new TempDirectory();
        long now = 1000;
        using var db = new AppDatabase(DatabasePath(directory), () => now);

        db.UpsertWorkspace(@"C:\work\repo");

        IReadOnlyList<WorkspaceRecord> records = db.GetWorkspaces();
        Assert.AreEqual(1, records.Count);
        Assert.AreEqual(@"C:\work\repo", records[0].Path);
        Assert.AreEqual(1000L, records[0].AddedAt);
        Assert.AreEqual(1000L, records[0].LastEditedAt);
    }

    [TestMethod]
    public void RepeatedUpsertKeepsOriginalAddedAt()
    {
        using var directory = new TempDirectory();
        long now = 1000;
        using var db = new AppDatabase(DatabasePath(directory), () => now);

        db.UpsertWorkspace(@"C:\work\repo");
        now = 2000;
        db.UpsertWorkspace(@"C:\work\repo");

        WorkspaceRecord record = db.GetWorkspaces().Single();
        Assert.AreEqual(1000L, record.AddedAt);
        Assert.AreEqual(2000L, record.LastEditedAt);
    }

    [TestMethod]
    public void TouchRefreshesLastEditedAt()
    {
        using var directory = new TempDirectory();
        long now = 1000;
        using var db = new AppDatabase(DatabasePath(directory), () => now);

        db.UpsertWorkspace(@"C:\work\repo");
        now = 2500;
        db.TouchLastEdited(@"C:\work\repo");

        WorkspaceRecord record = db.GetWorkspaces().Single();
        Assert.AreEqual(1000L, record.AddedAt);
        Assert.AreEqual(2500L, record.LastEditedAt);
    }

    [TestMethod]
    public void TouchOnUnknownPathRegistersTheWorkspace()
    {
        using var directory = new TempDirectory();
        long now = 3000;
        using var db = new AppDatabase(DatabasePath(directory), () => now);

        // Self-healing: touching an unregistered workspace still leaves a valid row.
        db.TouchLastEdited(@"C:\work\other");

        WorkspaceRecord record = db.GetWorkspaces().Single();
        Assert.AreEqual(3000L, record.AddedAt);
        Assert.AreEqual(3000L, record.LastEditedAt);
    }

    [TestMethod]
    public void RemoveDeletesTheRow()
    {
        using var directory = new TempDirectory();
        using var db = new AppDatabase(DatabasePath(directory));

        db.UpsertWorkspace(@"C:\work\repo");
        db.RemoveWorkspace(@"C:\work\repo");

        Assert.AreEqual(0, db.GetWorkspaces().Count);
    }

    [TestMethod]
    public void RowsPersistAcrossReopen()
    {
        using var directory = new TempDirectory();
        string path = DatabasePath(directory);

        using (var db = new AppDatabase(path, () => 1000))
        {
            db.UpsertWorkspace(@"C:\work\repo");
        }

        using var reopened = new AppDatabase(path, () => 9999);
        WorkspaceRecord record = reopened.GetWorkspaces().Single();
        Assert.AreEqual(@"C:\work\repo", record.Path);
        Assert.AreEqual(1000L, record.AddedAt);
    }

    [TestMethod]
    public void OpenMigratesToCurrentSchemaVersion()
    {
        using var directory = new TempDirectory();
        string path = DatabasePath(directory);
        using (var db = new AppDatabase(path))
        {
        }

        using var raw = new SqliteConnection($"Data Source={path}");
        raw.Open();
        Assert.AreEqual(AppDatabase.CurrentSchemaVersion, SqliteDatabase.ReadUserVersion(raw));
    }

    [TestMethod]
    public void OpenAppliesWalJournalMode()
    {
        using var directory = new TempDirectory();
        string path = DatabasePath(directory);
        using (var db = new AppDatabase(path))
        {
        }

        // WAL is a persistent property of the database file: a plain new
        // connection reports it without any PRAGMA replay.
        using var raw = new SqliteConnection($"Data Source={path}");
        raw.Open();
        Assert.AreEqual("wal", SqliteTestHelper.ScalarString(raw, "PRAGMA journal_mode;"));
    }
}
