using YKanBan.Storage.AppData;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Tests.ViewModels;

/// <summary>
/// Tests for the settings dialog: the theme is previewed as soon as it is
/// selected, saved on accept and restored on cancel.
/// </summary>
[TestClass]
public class SettingsDialogViewModelTests
{
    [TestMethod]
    public void OpeningTheDialogAppliesNothing()
    {
        var applied = new List<ThemeOption>();

        _ = new SettingsDialogViewModel(new AppConfig(), AppConfig.DefaultLanguage, applied.Add);

        Assert.AreEqual(0, applied.Count);
    }

    [TestMethod]
    public void SelectingAThemeAppliesItImmediatelyWithoutSavingIt()
    {
        var config = new AppConfig();
        var applied = new List<ThemeOption>();
        var dialog = new SettingsDialogViewModel(config, AppConfig.DefaultLanguage, applied.Add);

        dialog.SelectedTheme = dialog.Themes.Single(option => option.Value.Equals(ThemeOption.Dark));

        CollectionAssert.AreEqual(new[] { ThemeOption.Dark }, applied);
        Assert.AreEqual(ThemeOption.FollowSystem, config.Theme);
    }

    [TestMethod]
    public void AcceptSavesThePreviewedTheme()
    {
        var config = new AppConfig();
        var applied = new List<ThemeOption>();
        var dialog = new SettingsDialogViewModel(config, AppConfig.DefaultLanguage, applied.Add);
        dialog.SelectedTheme = dialog.Themes.Single(option => option.Value.Equals(ThemeOption.Light));

        dialog.Accept();

        Assert.AreEqual(ThemeOption.Light, config.Theme);
        CollectionAssert.AreEqual(new[] { ThemeOption.Light }, applied);
    }

    [TestMethod]
    public void CancelRestoresTheThemeTheDialogOpenedWith()
    {
        var config = new AppConfig { Theme = ThemeOption.Light };
        var applied = new List<ThemeOption>();
        var dialog = new SettingsDialogViewModel(config, AppConfig.DefaultLanguage, applied.Add);
        dialog.SelectedTheme = dialog.Themes.Single(option => option.Value.Equals(ThemeOption.Dark));

        dialog.Cancel();

        Assert.AreEqual(ThemeOption.Light, config.Theme);
        CollectionAssert.AreEqual(new[] { ThemeOption.Dark, ThemeOption.Light }, applied);
    }
}
