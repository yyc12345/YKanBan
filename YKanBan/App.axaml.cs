using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using YKanBan.Ipc;
using YKanBan.Services;
using YKanBan.Storage.AppData;
using YKanBan.ViewModels;
using YKanBan.Views;
using Resx = YKanBan.Resources;

namespace YKanBan;

public partial class App : Application
{
    /// <summary>Composition root, created before the main window.</summary>
    public static AppServices Services { get; } = new();

    /// <summary>The language applied at startup; a live selection differing from it needs a restart.</summary>
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

            // Startup order: apply the configured culture (and its RTL flow direction)
            // and the theme BEFORE creating the main window.
            CultureInfo culture = ResolveCulture(Services.Config.Language);
            StartupLanguage = Services.Config.Language;
            Resx.Culture = culture;
            ApplyTheme(Services.Config.Theme);

            var mainViewModel = new MainWindowViewModel(Services);
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,

                // RTL detection drives the whole window flow direction; the two
                // supported languages are LTR, but the infrastructure stays in
                // place for future languages.
                FlowDirection = culture.TextInfo.IsRightToLeft
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight,
            };

            // Primary instance: serve launch commands forwarded by secondary instances.
            Program.SingleInstance?.StartServer(command => HandleLaunchCommand(desktop, mainViewModel, command));
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Resolves the configured language to a culture, falling back to English.</summary>
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

    /// <summary>Applies a theme selection immediately; Default follows the operating system.</summary>
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

    /// <summary>Activates the window for a forwarded command; OpenWorkspace also switches to it.</summary>
    private static void HandleLaunchCommand(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindowViewModel mainViewModel,
        LaunchCommand command)
    {
        // IPC delivery happens on a background thread; hop to the UI thread.
        Dispatcher.UIThread.Post(() =>
        {
            if (desktop.MainWindow is { } window)
            {
                window.Show();
                window.Activate();
            }
            if (command is LaunchCommand.OpenWorkspace open)
            {
                mainViewModel.OpenWorkspaceFromLaunch(open.Path);
            }
        });
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
