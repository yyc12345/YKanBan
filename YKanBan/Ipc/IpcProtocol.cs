namespace YKanBan.Ipc;

/// <summary>
/// Text encoding of launch commands on the IPC pipe. A message is one or
/// two newline-terminated lines: "show", or "open" followed by the
/// workspace path taken verbatim (paths may contain spaces; paths
/// containing newlines are not supported). Decoding is strict and throws
/// FormatException on malformed payloads.
/// </summary>
public static class IpcProtocol
{
    /// <summary>Encodes a command into its wire text.</summary>
    /// <exception cref="InvalidOperationException">The command type is unknown.</exception>
    public static string Encode(LaunchCommand command) => command switch
    {
        LaunchCommand.ShowMain => "show\n",
        LaunchCommand.OpenWorkspace open => $"open\n{open.Path}\n",
        _ => throw new InvalidOperationException($"Unsupported launch command: {command?.GetType().Name ?? "null"}"),
    };

    /// <summary>Decodes wire text into a command; trailing newlines are tolerated.</summary>
    /// <exception cref="FormatException">The payload is not a valid message.</exception>
    public static LaunchCommand Decode(string payload)
    {
        List<string> lines = payload.Split('\n').ToList();

        // Trailing newlines are a framing detail; drop the empty lines they produce.
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            throw new FormatException("Empty IPC payload.");
        }

        switch (lines[0])
        {
            case "show":
                if (lines.Count != 1)
                {
                    throw new FormatException($"Unexpected content after 'show': '{lines[1]}'");
                }
                return new LaunchCommand.ShowMain();

            case "open":
                if (lines.Count != 2)
                {
                    throw new FormatException("'open' requires exactly one path line.");
                }
                if (lines[1].Length == 0)
                {
                    throw new FormatException("'open' requires a non-empty path.");
                }
                return new LaunchCommand.OpenWorkspace(lines[1]);

            default:
                throw new FormatException($"Unknown IPC command: '{lines[0]}'");
        }
    }
}
