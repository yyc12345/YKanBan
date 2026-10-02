using Microsoft.Data.Sqlite;
using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>
/// Tests for workspace detection and initialization. Initialization creates
/// the structure and an empty migrated database, but deliberately adds no
/// content.
/// </summary>
[TestClass]
public class WorkspaceInitializerTests
{
    [TestMethod]
    public void IsWorkspaceReflectsInitializationState()
    {
        using var directory = new TempDirectory();
        Assert.IsFalse(WorkspaceInitializer.IsWorkspace(directory.FullPath));

        WorkspaceInitializer.Initialize(directory.FullPath);
        Assert.IsTrue(WorkspaceInitializer.IsWorkspace(directory.FullPath));
    }

    [TestMethod]
    public void InitializeCreatesStructureAndMigratedEmptyDatabase()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        Assert.IsTrue(Directory.Exists(WorkspacePaths.Root(directory.FullPath)));
        Assert.IsTrue(File.Exists(WorkspacePaths.Database(directory.FullPath)));

        using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
        Assert.AreEqual(WorkspaceSchema.CurrentVersion, SqliteDatabase.ReadUserVersion(connection));
        Assert.IsTrue(SqliteTestHelper.TableExists(connection, "columns"));
        Assert.IsTrue(SqliteTestHelper.TableExists(connection, "cards"));
        Assert.IsTrue(SqliteTestHelper.TableExists(connection, "tags"));
        Assert.IsTrue(SqliteTestHelper.TableExists(connection, "card_tags"));
        // Initialization does not add preset content.
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM columns;"));
    }

    [TestMethod]
    public void InitializeMissingFolderThrows()
    {
        using var directory = new TempDirectory();
        string missing = Path.Combine(directory.FullPath, "not-created");

        WorkspaceDirectoryMissingException exception = Assert.ThrowsExactly<WorkspaceDirectoryMissingException>(
            () => WorkspaceInitializer.Initialize(missing));
        Assert.AreEqual(missing, exception.FolderPath);
    }

    [TestMethod]
    public void InitializeTwiceThrows()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceInitializer.Initialize(directory.FullPath));
    }
}
