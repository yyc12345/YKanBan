using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>Tests for seeding preset workspace content.</summary>
[TestClass]
public class WorkspacePresetTests
{
    [TestMethod]
    public void SeedAddsThreeDistinctPresetColumns()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);
        WorkspacePreset.SeedColumns(directory.FullPath);

        using var connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
        List<string> titles = SqliteTestHelper.GetColumnTitles(connection);

        Assert.AreEqual(3, titles.Count);
        Assert.AreEqual(titles.Count, titles.Distinct().Count());
        Assert.IsTrue(titles.All(title => title.Length > 0));
    }

    [TestMethod]
    public void SeedPresetColumnsShareOneTimestamp()
    {
        using var directory = new TempDirectory();
        WorkspaceInitializer.Initialize(directory.FullPath);
        WorkspacePreset.SeedColumns(directory.FullPath);

        using var connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);

        // Creation writes created_at = updated_at; all three columns share one moment.
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM columns WHERE created_at <> updated_at;"));
        Assert.AreEqual(3L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM columns WHERE created_at > 0;"));
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(DISTINCT created_at) FROM columns;"));
    }
}
