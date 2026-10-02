using System.Diagnostics.CodeAnalysis;

namespace YKanBan.Launching;

/// <summary>
/// Parses raw command-line arguments. The only accepted form is a single,
/// non-blank workspace path (<c>ykanban &lt;path&gt;</c>); zero arguments,
/// more than one argument, or a blank argument is rejected as an argument
/// error. The path itself is not checked for existence here.
/// </summary>
public static class LaunchArguments
{
    /// <summary>
    /// Tries to parse command-line arguments following the
    /// <c>ykanban &lt;path&gt;</c> contract.
    /// </summary>
    /// <param name="arguments">The raw arguments, excluding the executable name.</param>
    /// <param name="folderPath">The parsed workspace path on success, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when exactly one non-blank path was supplied.</returns>
    public static bool TryParse(IReadOnlyList<string> arguments, [NotNullWhen(true)] out string? folderPath)
    {
        if (arguments.Count == 1 && !string.IsNullOrWhiteSpace(arguments[0]))
        {
            folderPath = arguments[0];
            return true;
        }

        folderPath = null;
        return false;
    }
}
