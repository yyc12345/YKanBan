using YKanBan.Storage.AppData;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.AppData;

/// <summary>Tests for app.json load/save and its degradation rules.</summary>
[TestClass]
public class AppConfigStoreTests
{
    [TestMethod]
    public void MissingFileYieldsDefaults()
    {
        using var directory = new TempDirectory();
        var store = new AppConfigStore(Path.Combine(directory.FullPath, "app.json"));

        AppConfig config = store.Load();
        Assert.AreEqual(AppConfig.CurrentFormatVersion, config.Version);
        Assert.AreEqual("en", config.Language);
        Assert.AreEqual(ThemeOption.FollowSystem, config.Theme);
        Assert.AreEqual(CardSortOption.Id, config.Sort.Card);
        Assert.AreEqual(SidebarSortOption.NameAsc, config.Sort.Sidebar);
        Assert.AreEqual(ColumnWidthPreset.Standard, config.ColumnWidth.Preset);
        Assert.AreEqual(320, config.ColumnWidth.CustomPixels);
        Assert.IsTrue(config.Confirmations.DeleteCard);
        Assert.IsTrue(config.Confirmations.DeleteColumn);
        Assert.IsTrue(config.Confirmations.DeleteTag);
        Assert.IsTrue(config.Confirmations.DiscardEdit);
    }

    [TestMethod]
    public void CorruptJsonIsBackedUpAndDefaultsAreUsed()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, "{{{ not json");
        var store = new AppConfigStore(path);

        AppConfig config = store.Load();

        Assert.AreEqual("en", config.Language);
        Assert.IsTrue(File.Exists(path + ".bak"));
        Assert.AreEqual("{{{ not json", File.ReadAllText(path + ".bak"));
    }

    [TestMethod]
    public void WrongShapeJsonIsBackedUpAndDefaultsAreUsed()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, "[1, 2, 3]");
        var store = new AppConfigStore(path);

        AppConfig config = store.Load();

        Assert.AreEqual(AppConfig.CurrentFormatVersion, config.Version);
        Assert.IsTrue(File.Exists(path + ".bak"));
    }

    [TestMethod]
    public void SaveAndLoadRoundTripsAllSettings()
    {
        using var directory = new TempDirectory();
        var store = new AppConfigStore(Path.Combine(directory.FullPath, "app.json"));

        store.Save(new AppConfig
        {
            Language = "zh-Hans",
            Theme = ThemeOption.Dark,
            Sort = new SortSettings
            {
                Card = CardSortOption.Title,
                Sidebar = SidebarSortOption.RecentlyUpdated,
            },
            ColumnWidth = new ColumnWidthSettings
            {
                Preset = ColumnWidthPreset.Custom,
                CustomPixels = 500,
            },
            Confirmations = new ConfirmationSettings
            {
                DeleteCard = false,
                DeleteColumn = false,
                DeleteTag = false,
                DiscardEdit = false,
            },
        });
        AppConfig loaded = store.Load();

        Assert.AreEqual("zh-Hans", loaded.Language);
        Assert.AreEqual(ThemeOption.Dark, loaded.Theme);
        Assert.AreEqual(CardSortOption.Title, loaded.Sort.Card);
        Assert.AreEqual(SidebarSortOption.RecentlyUpdated, loaded.Sort.Sidebar);
        Assert.AreEqual(ColumnWidthPreset.Custom, loaded.ColumnWidth.Preset);
        Assert.AreEqual(500, loaded.ColumnWidth.CustomPixels);
        Assert.IsFalse(loaded.Confirmations.DeleteCard);
        Assert.IsFalse(loaded.Confirmations.DeleteColumn);
        Assert.IsFalse(loaded.Confirmations.DeleteTag);
        Assert.IsFalse(loaded.Confirmations.DiscardEdit);
    }

    [TestMethod]
    public void SaveWritesKebabCaseNamesAndVersionField()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        var store = new AppConfigStore(path);

        store.Save(new AppConfig());
        string json = File.ReadAllText(path);

        // Keys and enum values are serialized in kebab-case; "version" is the migration hook.
        Assert.IsTrue(json.Contains($"\"version\": {AppConfig.CurrentFormatVersion}"));
        Assert.IsTrue(json.Contains("\"language\": \"en\""));
        Assert.IsTrue(json.Contains("\"theme\": \"follow-system\""));
        Assert.IsTrue(json.Contains("\"sort\":"));
        Assert.IsTrue(json.Contains("\"card\": \"id\""));
        Assert.IsTrue(json.Contains("\"sidebar\": \"name-asc\""));
        Assert.IsTrue(json.Contains("\"column-width\":"));
        Assert.IsTrue(json.Contains("\"preset\": \"standard\""));
        Assert.IsTrue(json.Contains("\"custom-pixels\": 320"));
        Assert.IsTrue(json.Contains("\"confirmations\":"));
        Assert.IsTrue(json.Contains("\"delete-card\": true"));
        Assert.IsTrue(json.Contains("\"delete-column\": true"));
        Assert.IsTrue(json.Contains("\"delete-tag\": true"));
        Assert.IsTrue(json.Contains("\"discard-edit\": true"));
    }

    [TestMethod]
    public void MissingSectionsFallBackToTheirDefaults()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, $$"""
            {
              "version": {{AppConfig.CurrentFormatVersion}},
              "language": "zh-Hans"
            }
            """);
        var store = new AppConfigStore(path);

        AppConfig config = store.Load();

        // The present field loads; absent sections each fall back to defaults.
        Assert.AreEqual("zh-Hans", config.Language);
        Assert.AreEqual(CardSortOption.Id, config.Sort.Card);
        Assert.AreEqual(ColumnWidthPreset.Standard, config.ColumnWidth.Preset);
        Assert.IsTrue(config.Confirmations.DeleteCard);
    }

    [TestMethod]
    public void LoadPreservesUnknownFieldsTolerantly()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        // A newer version writes an unknown field plus a known one.
        File.WriteAllText(path, $$"""
            {
              "version": {{AppConfig.CurrentFormatVersion}},
              "language": "zh-Hans",
              "some-future-field": 42
            }
            """);
        var store = new AppConfigStore(path);

        AppConfig config = store.Load();

        Assert.AreEqual("zh-Hans", config.Language);
    }
}
