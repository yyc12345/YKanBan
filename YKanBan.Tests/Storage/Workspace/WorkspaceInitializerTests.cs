using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>Tests for .ykanban folder and empty database creation.</summary>
[TestClass]
public class WorkspaceInitializerTests
{
    [TestMethod]
    public void InitializeCreatesYkanbanFolderAndDatabase()
    {
        using var directory = new TempDirectory();

        Assert.IsFalse(WorkspaceInitializer.IsWorkspace(directory.FullPath));
        WorkspaceInitializer.Initialize(directory.FullPath);

        Assert.IsTrue(WorkspaceInitializer.IsWorkspace(directory.FullPath));
        Assert.IsTrue(File.Exists(WorkspacePaths.Database(directory.FullPath)));
        // The lock file only appears once a WorkspaceLock is acquired.
        Assert.IsFalse(File.Exists(WorkspacePaths.LockFile(directory.FullPath)));
    }

    [TestMethod]
    public void InitializeLeavesDatabaseEmpty()
    {
        // Initialization creates structure only; preset content is seeded separately.
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        using var connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM columns;"));
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM cards;"));
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM tags;"));
    }

    [TestMethod]
    public void InitializeThrowsWhenDirectoryIsMissing()
    {
        string missing = Path.Combine(Path.GetTempPath(), "ykanban-tests-" + Guid.NewGuid().ToString("N"));
        Assert.ThrowsExactly<WorkspaceDirectoryMissingException>(() => WorkspaceInitializer.Initialize(missing));
    }

    [TestMethod]
    public void InitializeThrowsWhenDatabaseAlreadyExists()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceInitializer.Initialize(directory.FullPath));
    }
}
