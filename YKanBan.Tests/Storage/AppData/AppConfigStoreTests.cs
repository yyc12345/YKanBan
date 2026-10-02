using YKanBan.Storage.AppData;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.AppData;

/// <summary>
/// Tests for app.json round-tripping, kebab-case naming and graceful
/// degradation on a missing or corrupt file.
/// </summary>
[TestClass]
public class AppConfigStoreTests
{
    [TestMethod]
    public void LoadMissingFileReturnsDefaults()
    {
        using var directory = new TempDirectory();
        var store = new AppConfigStore(Path.Combine(directory.FullPath, "app.json"));

        AppConfig config = store.Load();

        Assert.AreEqual(AppConfig.CurrentFormatVersion, config.Version);
        Assert.AreEqual(AppConfig.DefaultLanguage, config.Language);
        Assert.AreEqual(ThemeOption.FollowSystem, config.Theme);
        Assert.AreEqual(CardSortOption.Id, config.Sort.Card);
        Assert.AreEqual(ColumnWidthPreset.Standard, config.ColumnWidth.Preset);
        Assert.AreEqual(320, config.ColumnWidth.CustomPixels);
        Assert.IsTrue(config.Confirmations.DeleteCard);
        Assert.IsTrue(config.Confirmations.DeleteColumn);
        Assert.IsTrue(config.Confirmations.DeleteTag);
        Assert.IsTrue(config.Confirmations.DiscardEdit);
    }

    [TestMethod]
    public void LoadCorruptFileBacksUpAndReturnsDefaults()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, "{ this is not valid json");
        var store = new AppConfigStore(path);

        AppConfig config = store.Load();

        Assert.AreEqual(AppConfig.CurrentFormatVersion, config.Version);
        Assert.IsTrue(File.Exists(path + ".bak"));
        Assert.AreEqual("{ this is not valid json", File.ReadAllText(path + ".bak"));
    }

    [TestMethod]
    public void LoadJsonNullReturnsDefaults()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, "null");
        var store = new AppConfigStore(path);

        Assert.AreEqual(AppConfig.CurrentFormatVersion, store.Load().Version);
    }

    [TestMethod]
    public void LoadReadsKebabCaseKeysAndEnumValues()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, $$"""
            {
              "version": {{AppConfig.CurrentFormatVersion}},
              "language": "zh-Hans",
              "theme": "dark",
              "sort": { "card": "title" },
              "column-width": { "preset": "custom", "custom-pixels": 400 },
              "confirmations": {
                "delete-card": false,
                "delete-column": false,
                "delete-tag": false,
                "discard-edit": false
              }
            }
            """);
        var store = new AppConfigStore(path);

        AppConfig config = store.Load();

        Assert.AreEqual("zh-Hans", config.Language);
        Assert.AreEqual(ThemeOption.Dark, config.Theme);
        Assert.AreEqual(CardSortOption.Title, config.Sort.Card);
        Assert.AreEqual(ColumnWidthPreset.Custom, config.ColumnWidth.Preset);
        Assert.AreEqual(400, config.ColumnWidth.CustomPixels);
        Assert.IsFalse(config.Confirmations.DeleteCard);
        Assert.IsFalse(config.Confirmations.DeleteColumn);
        Assert.IsFalse(config.Confirmations.DeleteTag);
        Assert.IsFalse(config.Confirmations.DiscardEdit);
    }

    [TestMethod]
    public void LoadMissingSectionsFillsDefaults()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, $$"""{ "version": {{AppConfig.CurrentFormatVersion}} }""");
        var store = new AppConfigStore(path);

        AppConfig config = store.Load();

        Assert.IsNotNull(config.Sort);
        Assert.IsNotNull(config.ColumnWidth);
        Assert.IsNotNull(config.Confirmations);
        Assert.AreEqual(ColumnWidthPreset.Standard, config.ColumnWidth.Preset);
    }

    [TestMethod]
    public void SaveWritesKebabCaseKeysAndEnumValues()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        var store = new AppConfigStore(path);

        var config = new AppConfig
        {
            Theme = ThemeOption.FollowSystem,
            Sort = new SortSettings { Card = CardSortOption.UpdatedAt },
            ColumnWidth = new ColumnWidthSettings { Preset = ColumnWidthPreset.Custom, CustomPixels = 500 },
            Confirmations = new ConfirmationSettings { DeleteColumn = false },
        };
        store.Save(config);

        string json = File.ReadAllText(path);
        StringAssert.Contains(json, "\"delete-column\"");
        StringAssert.Contains(json, "\"discard-edit\"");
        StringAssert.Contains(json, "\"custom-pixels\"");
        StringAssert.Contains(json, "\"follow-system\"");
        StringAssert.Contains(json, "\"updated-at\"");
        // No member-name CamelCase must leak into the file.
        Assert.IsFalse(json.Contains("deleteColumn", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("CustomPixels", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SaveThenLoadRoundTrips()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        var store = new AppConfigStore(path);

        var config = new AppConfig
        {
            Language = "zh-Hans",
            Theme = ThemeOption.Light,
            Sort = new SortSettings { Card = CardSortOption.CreatedAt },
            ColumnWidth = new ColumnWidthSettings { Preset = ColumnWidthPreset.Wide },
            Confirmations = new ConfirmationSettings { DiscardEdit = false },
        };
        store.Save(config);

        AppConfig reloaded = store.Load();
        Assert.AreEqual(config.Language, reloaded.Language);
        Assert.AreEqual(config.Theme, reloaded.Theme);
        Assert.AreEqual(config.Sort.Card, reloaded.Sort.Card);
        Assert.AreEqual(config.ColumnWidth.Preset, reloaded.ColumnWidth.Preset);
        Assert.AreEqual(config.Confirmations.DiscardEdit, reloaded.Confirmations.DiscardEdit);
    }
}
