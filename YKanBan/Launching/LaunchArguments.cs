using System.Diagnostics.CodeAnalysis;

namespace YKanBan.Launching;

/// <summary>
/// Parses raw command-line arguments. The only accepted form is a single,
/// non-blank workspace path (<c>ykanban &lt;path&gt;</c>); zero arguments,
/// more than one argument, a blank argument, an argument starting with
/// <c>-</c> (any option form, <c>--help</c> included) or a malformed path is
/// rejected as an argument error. A relative path is resolved against the
/// process's current directory; the path itself is not checked for existence
/// here.
/// </summary>
public static class LaunchArguments
{
    /// <summary>
    /// Tries to parse command-line arguments following the
    /// <c>ykanban &lt;path&gt;</c> contract.
    /// </summary>
    /// <param name="arguments">The raw arguments, excluding the executable name.</param>
    /// <param name="folderPath">The absolute workspace path on success, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when exactly one usable path was supplied.</returns>
    public static bool TryParse(IReadOnlyList<string> arguments, [NotNullWhen(true)] out string? folderPath)
    {
        folderPath = null;
        if (arguments.Count != 1 || string.IsNullOrWhiteSpace(arguments[0]) || arguments[0].StartsWith('-'))
        {
            return false;
        }

        try
        {
            folderPath = Path.GetFullPath(arguments[0]);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path the OS cannot even represent is a bad argument, not a missing folder.
            return false;
        }
    }
}
