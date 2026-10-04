using System.Globalization;

using Microsoft.Data.Sqlite;

using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>
/// Verifies that the preset columns and tags follow the UI language active when
/// they are added and then live as ordinary data. These tests mutate the global
/// <see cref="Resources.Culture"/>, so they are marked do-not-parallelize.
/// </summary>
[TestClass]
[DoNotParallelize]
public class WorkspacePresetTests {
    [TestMethod]
    public void AddPresetColumnsStoresEnglishTextUnderEnglishCulture() {
        CultureInfo? original = Resources.Culture;
        try {
            Resources.Culture = new CultureInfo("en");

            using var directory = new TempDirectory();
            WorkspaceInitializer.Initialize(directory.FullPath);
            WorkspacePreset.AddPresetColumns(directory.FullPath);

            using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
            CollectionAssert.AreEqual(
                new[]
                {
                    Resources.PresetColumn_Todo,
                    Resources.PresetColumn_InProgress,
                    Resources.PresetColumn_Done,
                },
                SqliteTestHelper.GetColumnTitles(connection));
            CollectionAssert.AreEqual(
                new[]
                {
                    Resources.PresetColumn_Todo_Description,
                    Resources.PresetColumn_InProgress_Description,
                    Resources.PresetColumn_Done_Description,
                },
                SqliteTestHelper.GetColumnDescriptions(connection));
        } finally {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void AddPresetColumnsStoresChineseTextUnderChineseCulture() {
        CultureInfo? original = Resources.Culture;
        try {
            Resources.Culture = new CultureInfo("zh-Hans");

            using var directory = new TempDirectory();
            WorkspaceInitializer.Initialize(directory.FullPath);
            WorkspacePreset.AddPresetColumns(directory.FullPath);

            using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
            CollectionAssert.AreEqual(
                new[]
                {
                    Resources.PresetColumn_Todo,
                    Resources.PresetColumn_InProgress,
                    Resources.PresetColumn_Done,
                },
                SqliteTestHelper.GetColumnTitles(connection));
            // Chinese resources must resolve to different text than English.
            Assert.AreEqual("待办", Resources.PresetColumn_Todo);
            Assert.AreEqual("已完成的任务。", Resources.PresetColumn_Done_Description);
        } finally {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void AddPresetTagsStoresEightTagsUnderEnglishCulture() {
        CultureInfo? original = Resources.Culture;
        try {
            Resources.Culture = new CultureInfo("en");

            using var directory = new TempDirectory();
            WorkspaceInitializer.Initialize(directory.FullPath);
            WorkspacePreset.AddPresetTags(directory.FullPath);

            using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
            List<(string Name, string Color, string Description)> tags = SqliteTestHelper.GetTags(connection);

            Assert.AreEqual(8, tags.Count);
            Assert.AreEqual(("bug", "#d73a4a", "Something isn't working"), tags[0]);
            Assert.AreEqual(("documentation", "#0075ca", "Improvements or additions to documentation"), tags[1]);
            Assert.AreEqual(("wontfix", "#ffffff", "This will not be worked on"), tags[7]);
        } finally {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void AddPresetTagsStoresChineseNamesUnderChineseCulture() {
        CultureInfo? original = Resources.Culture;
        try {
            Resources.Culture = new CultureInfo("zh-Hans");

            using var directory = new TempDirectory();
            WorkspaceInitializer.Initialize(directory.FullPath);
            WorkspacePreset.AddPresetTags(directory.FullPath);

            using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
            List<(string Name, string Color, string Description)> tags = SqliteTestHelper.GetTags(connection);

            Assert.AreEqual(8, tags.Count);
            Assert.AreEqual("缺陷", tags[0].Name);
            Assert.AreEqual("功能没有正常工作", tags[0].Description);
        } finally {
            Resources.Culture = original;
        }
    }

    [TestMethod]
    public void PresetContentIsDataAndIgnoresLaterCultureChanges() {
        CultureInfo? original = Resources.Culture;
        try {
            Resources.Culture = new CultureInfo("en");

            using var directory = new TempDirectory();
            WorkspaceInitializer.Initialize(directory.FullPath);
            WorkspacePreset.AddPresetColumns(directory.FullPath);
            WorkspacePreset.AddPresetTags(directory.FullPath);

            // Switching the language after adding the presets must not rewrite stored data.
            Resources.Culture = new CultureInfo("zh-Hans");
            using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(directory.FullPath);
            Assert.AreEqual("To Do", SqliteTestHelper.GetColumnTitles(connection)[0]);
            Assert.AreEqual("bug", SqliteTestHelper.GetTags(connection)[0].Name);
        } finally {
            Resources.Culture = original;
        }
    }
}
