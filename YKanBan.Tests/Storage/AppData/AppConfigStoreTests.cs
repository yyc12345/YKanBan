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
    public void LoadUnreadableFileReturnsDefaults()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, "{ \"language\": \"zh-Hans\" }");
        var store = new AppConfigStore(path);

        AppConfig config;
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            config = store.Load();
        }

        Assert.AreEqual(AppConfig.DefaultLanguage, config.Language);
        Assert.IsFalse(File.Exists(path + ".bak"));
    }

    [TestMethod]
    public void LoadCorruptFileReturnsDefaultsWhenTheBackupCannotBeWritten()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, "{ this is not valid json");
        File.WriteAllText(path + ".bak", "older backup");
        var store = new AppConfigStore(path);

        AppConfig config;
        using (new FileStream(path + ".bak", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            config = store.Load();
        }

        Assert.AreEqual(AppConfig.CurrentFormatVersion, config.Version);
        Assert.AreEqual("older backup", File.ReadAllText(path + ".bak"));
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
            Sort = new SortSettings { Card = CardSortOption.Title },
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

    [TestMethod]
    public void LoadNonObjectRootBacksUpAndReturnsDefaults()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, "[1, 2]");
        var store = new AppConfigStore(path);

        Assert.AreEqual(AppConfig.DefaultLanguage, store.Load().Language);
        Assert.AreEqual("[1, 2]", File.ReadAllText(path + ".bak"));
    }

    [TestMethod]
    [DataRow("\"theme\": \"sepia\"")]
    [DataRow("\"theme\": 1")]
    [DataRow("\"theme\": {}")]
    [DataRow("\"language\": \"fr\"")]
    [DataRow("\"language\": \"zh-hans\"")]
    [DataRow("\"language\": 5")]
    [DataRow("\"sort\": { \"card\": \"created-at\" }")]
    [DataRow("\"sort\": 7")]
    [DataRow("\"column-width\": { \"preset\": \"huge\", \"custom-pixels\": 159 }")]
    [DataRow("\"column-width\": { \"custom-pixels\": 721 }")]
    [DataRow("\"column-width\": { \"custom-pixels\": \"wide\" }")]
    [DataRow("\"confirmations\": { \"delete-card\": \"nope\" }")]
    [DataRow("\"version\": \"one\"")]
    public void InvalidFieldFallsBackAloneWithoutBackup(string invalidMember)
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");

        // The valid fields are non-defaults, so any whole-file fallback would show.
        File.WriteAllText(path, $$"""
            {
              "theme": "dark",
              "confirmations": { "discard-edit": false },
              {{invalidMember}}
            }
            """);
        var store = new AppConfigStore(path);

        AppConfig config = store.Load();

        Assert.IsFalse(File.Exists(path + ".bak"));
        AppConfig defaults = new();
        Assert.AreEqual(invalidMember.Contains("theme") ? defaults.Theme : ThemeOption.Dark, config.Theme);
        if (!invalidMember.Contains("confirmations"))
        {
            Assert.IsFalse(config.Confirmations.DiscardEdit);
        }
        Assert.AreEqual(defaults.Language, config.Language);
        Assert.AreEqual(defaults.Sort.Card, config.Sort.Card);
        Assert.AreEqual(defaults.ColumnWidth.Preset, config.ColumnWidth.Preset);
        Assert.AreEqual(defaults.ColumnWidth.CustomPixels, config.ColumnWidth.CustomPixels);
        Assert.AreEqual(defaults.Confirmations.DeleteCard, config.Confirmations.DeleteCard);
        Assert.AreEqual(defaults.Version, config.Version);
    }

    [TestMethod]
    public void InvalidNestedFieldKeepsItsSiblings()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        File.WriteAllText(path, """{ "column-width": { "preset": "bogus", "custom-pixels": 500 } }""");

        AppConfig config = new AppConfigStore(path).Load();

        Assert.AreEqual(ColumnWidthPreset.Standard, config.ColumnWidth.Preset);
        Assert.AreEqual(500, config.ColumnWidth.CustomPixels);
    }

    [TestMethod]
    public void ColumnWidthBoundsAreInclusive()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");

        File.WriteAllText(path, """{ "column-width": { "custom-pixels": 160 } }""");
        Assert.AreEqual(160, new AppConfigStore(path).Load().ColumnWidth.CustomPixels);

        File.WriteAllText(path, """{ "column-width": { "custom-pixels": 720 } }""");
        Assert.AreEqual(720, new AppConfigStore(path).Load().ColumnWidth.CustomPixels);
    }

    [TestMethod]
    public void UnchangedConfigurationIsNotWritten()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        var store = new AppConfigStore(path);

        AppConfig config = store.Load();

        Assert.IsFalse(store.SaveIfChanged(config));
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void FieldFallbackAloneDoesNotRewriteTheFile()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        const string original = """{ "theme": "sepia" }""";
        File.WriteAllText(path, original);
        var store = new AppConfigStore(path);

        AppConfig config = store.Load();

        Assert.IsFalse(store.SaveIfChanged(config));
        Assert.AreEqual(original, File.ReadAllText(path));
    }

    [TestMethod]
    public void ChangedConfigurationIsWrittenOnce()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        var store = new AppConfigStore(path);
        AppConfig config = store.Load();

        config.Sort.Card = CardSortOption.UpdatedAt;

        Assert.IsTrue(store.SaveIfChanged(config));
        Assert.AreEqual(CardSortOption.UpdatedAt, new AppConfigStore(path).Load().Sort.Card);

        // Saved state is the new baseline.
        Assert.IsFalse(store.SaveIfChanged(config));
    }

    [TestMethod]
    public void ChangeRevertedWithinSessionIsNotWritten()
    {
        using var directory = new TempDirectory();
        string path = Path.Combine(directory.FullPath, "app.json");
        var store = new AppConfigStore(path);
        AppConfig config = store.Load();

        config.Theme = ThemeOption.Dark;
        config.Theme = ThemeOption.FollowSystem;

        Assert.IsFalse(store.SaveIfChanged(config));
        Assert.IsFalse(File.Exists(path));
    }
}
