using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using YKanBan.Ipc;
using YKanBan.ViewModels;
using YKanBan.Views;

namespace YKanBan;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            // Primary instance: serve launch commands forwarded by secondary instances.
            Program.SingleInstance?.StartServer(command => HandleLaunchCommand(desktop, command));
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Brings the main window to front for a forwarded launch command. Both
    /// ShowMain and OpenWorkspace activate the window; switching to the
    /// requested workspace lands together with the workspace UI.
    /// </summary>
    private static void HandleLaunchCommand(IClassicDesktopStyleApplicationLifetime desktop, LaunchCommand command)
    {
        // IPC delivery happens on a background thread; window access must hop to the UI thread.
        Dispatcher.UIThread.Post(() =>
        {
            if (desktop.MainWindow is { } window)
            {
                window.Show();
                window.Activate();
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
