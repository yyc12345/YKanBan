namespace YKanBan.Ipc;

/// <summary>Parses command-line arguments into a launch command.</summary>
public static class LaunchArguments
{
    /// <summary>
    /// Parses raw command-line arguments: with no meaningful argument the
    /// command is ShowMain; otherwise the first non-blank argument is taken
    /// as the workspace path and any further arguments are ignored.
    /// </summary>
    public static LaunchCommand Parse(IReadOnlyList<string> arguments)
    {
        string? path = arguments.FirstOrDefault(argument => !string.IsNullOrWhiteSpace(argument));
        return path is null
            ? new LaunchCommand.ShowMain()
            : new LaunchCommand.OpenWorkspace(path);
    }
}
