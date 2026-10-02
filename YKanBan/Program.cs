using System;
using Avalonia;
using YKanBan.Ipc;

namespace YKanBan;

sealed class Program
{
    /// <summary>The single-instance service held by this process while the app runs.</summary>
    internal static ISingleInstanceService? SingleInstance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        LaunchCommand command = LaunchArguments.Parse(args);
        using ISingleInstanceService singleInstance = new SingleInstanceService();

        // Secondary instance: forward the command to the primary, then exit immediately.
        if (!singleInstance.TryAcquireSingleInstance())
        {
            singleInstance.TrySend(command);
            return;
        }

        SingleInstance = singleInstance;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        // Lifetime ended: persist the configuration once, then release the services.
        App.Services.SaveConfiguration();
        App.Services.Dispose();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
