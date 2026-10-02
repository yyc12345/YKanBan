using System.Globalization;
using Microsoft.Data.Sqlite;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>
/// Verifies that the preset column titles follow the UI language active when
/// they are added and then live as ordinary data. These tests mutate the
/// global <see cref="Resources.Culture"/>, so they are marked do-not-parallelize.
/// </summary>
[TestClass]
[DoNotParallelize]
public class WorkspacePresetTests
{
    [TestMethod]
    public void AddPresetColumnsStoresEnglishTitlesUnderEnglishCulture()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            string[] expected =
            [
                Resources.PresetColumn_Todo,
                Resources.PresetColumn_InProgress,
                Resources.PresetColumn_Done,
            ];

            using var directory = new TempDirectory();
            WorkspaceInitializer.Initialize(directory.FullPath);
            WorkspacePreset.AddPresetColumns(directory.FullPath);

            using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
            CollectionAssert.AreEqual(expected, SqliteTestHelper.GetColumnTitles(connection));
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void AddPresetColumnsStoresChineseTitlesUnderChineseCulture()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("zh-Hans");
            string[] expected =
            [
                Resources.PresetColumn_Todo,
                Resources.PresetColumn_InProgress,
                Resources.PresetColumn_Done,
            ];

            using var directory = new TempDirectory();
            WorkspaceInitializer.Initialize(directory.FullPath);
            WorkspacePreset.AddPresetColumns(directory.FullPath);

            using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
            CollectionAssert.AreEqual(expected, SqliteTestHelper.GetColumnTitles(connection));
        }
        finally
        {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void PresetTitlesAreDataAndIgnoreLaterCultureChanges()
    {
        CultureInfo? original = Resources.Culture;
        try
        {
            Resources.Culture = new CultureInfo("en");
            string[] expected =
            [
                Resources.PresetColumn_Todo,
                Resources.PresetColumn_InProgress,
                Resources.PresetColumn_Done,
            ];

            using var directory = new TempDirectory();
            WorkspaceInitializer.Initialize(directory.FullPath);
            WorkspacePreset.AddPresetColumns(directory.FullPath);

            // Switching the language after adding the presets must not rewrite stored data.
            Resources.Culture = new CultureInfo("zh-Hans");
            using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
            CollectionAssert.AreEqual(expected, SqliteTestHelper.GetColumnTitles(connection));
        }
        finally
        {
            Resources.Culture = original;
        }
    }
}
