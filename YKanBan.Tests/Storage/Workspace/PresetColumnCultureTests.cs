using System.Globalization;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>
/// Verifies that preset column titles follow the UI language active at
/// seeding time and then live as ordinary data. Mutates the global
/// Resources.Culture, therefore marked DoNotParallelize.
/// </summary>
[TestClass]
[DoNotParallelize]
public class PresetColumnCultureTests
{
    [TestMethod]
    public void PresetTitlesAreEnglishUnderEnglishCulture()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            using var directory = new TempDirectory();
            WorkspaceInitializer.Initialize(directory.FullPath);
            WorkspacePreset.SeedColumns(directory.FullPath);

            using var connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
            CollectionAssert.AreEqual(
                new[] { "To Do", "In Progress", "Done" },
                SqliteTestHelper.GetColumnTitles(connection));
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void PresetTitlesAreChineseUnderChineseCulture()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("zh-Hans");
            using var directory = new TempDirectory();
            WorkspaceInitializer.Initialize(directory.FullPath);
            WorkspacePreset.SeedColumns(directory.FullPath);

            using var connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
            CollectionAssert.AreEqual(
                new[] { "待办", "进行中", "已完成" },
                SqliteTestHelper.GetColumnTitles(connection));
        }
        finally
        {
            Resources.Culture = original;
        }
    }
}
