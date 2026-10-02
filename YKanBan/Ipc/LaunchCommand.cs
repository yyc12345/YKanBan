namespace YKanBan.Ipc;

/// <summary>
/// A launch command: what a YKanBan invocation asks the application to do.
/// Doubles as the IPC message forwarded from a secondary instance to the
/// primary one.
/// </summary>
public abstract record LaunchCommand
{
    /// <summary>No arguments: show the main window and bring it to front.</summary>
    public sealed record ShowMain : LaunchCommand;

    /// <summary>A workspace path: activate the window and switch to (or open) the workspace.</summary>
    public sealed record OpenWorkspace(string Path) : LaunchCommand;
}
