using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using YKanBan.Services;
using YKanBan.Storage.AppData;
using YKanBan.ViewModels;
using YKanBan.Views;
using Resx = YKanBan.Resources;

namespace YKanBan;

public partial class App : Application
{
    /// <summary>
    /// Gets the process-wide composition root, created at startup.
    /// </summary>
    public static AppServices? Services { get; private set; }

    /// <summary>
    /// Gets the language applied at startup; a live selection differing from it
    /// needs a restart.
    /// </summary>
    public static string StartupLanguage { get; private set; } = AppConfig.DefaultLanguage;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // Startup order: apply the configured culture, its RTL flow direction
            // and the theme BEFORE creating the main window.
            Services = new AppServices();
            CultureInfo culture = ResolveCulture(Services.Config.Language);
            StartupLanguage = Services.Config.Language;
            Resx.Culture = culture;
            ApplyTheme(Services.Config.Theme);

            var viewModel = new MainWindowViewModel(Services, desktop.Args ?? []);
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,

                // RTL detection drives the whole window flow direction; the two
                // supported languages are LTR, but the infrastructure stays in
                // place for future languages.
                FlowDirection = culture.TextInfo.IsRightToLeft
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Resolves a configured language to a culture, falling back to English.
    /// </summary>
    /// <param name="language">The language code from app.json.</param>
    /// <returns>The resolved culture.</returns>
    public static CultureInfo ResolveCulture(string language)
    {
        try
        {
            return new CultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return new CultureInfo(AppConfig.DefaultLanguage);
        }
    }

    /// <summary>
    /// Applies a theme selection immediately; <see cref="ThemeOption.FollowSystem"/>
    /// inherits the operating system preference.
    /// </summary>
    /// <param name="option">The theme selection.</param>
    public static void ApplyTheme(ThemeOption option)
    {
        if (Current is null)
        {
            return;
        }

        Current.RequestedThemeVariant = option switch
        {
            ThemeOption.Light => ThemeVariant.Light,
            ThemeOption.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
