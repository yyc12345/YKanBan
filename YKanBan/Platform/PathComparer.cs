namespace YKanBan.Platform;

/// <summary>
/// Compares and normalizes workspace folder paths. Normalization produces
/// the canonical absolute form: separators unified by the operating system,
/// "." and ".." collapsed, trailing separator trimmed except on roots.
/// Symbolic links are deliberately NOT resolved. Case handling is an
/// injected platform rule — ignored on Windows, significant on Unix and
/// macOS — so the Unix behavior is testable on any host.
/// </summary>
public sealed class PathComparer : IEqualityComparer<string>
{
    private readonly StringComparison _comparison;

    /// <summary>Creates a comparer with an explicit case-sensitivity rule.</summary>
    /// <param name="ignoreCase">True for Windows-style path semantics.</param>
    public PathComparer(bool ignoreCase)
    {
        _comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    /// <summary>The comparer matching the current operating system's path semantics.</summary>
    public static PathComparer ForCurrentPlatform() => new(OperatingSystem.IsWindows());

    /// <summary>Returns the canonical absolute form used for storage and display.</summary>
    public string Normalize(string path)
    {
        // GetFullPath makes the path absolute, collapses relative segments and
        // unifies separators; it preserves a trailing separator, so trim it to
        // keep the canonical form unique ("D:\repo" and "D:\repo\" are one path).
        string full = Path.GetFullPath(path);
        return TrimTrailingSeparator(full);
    }

    /// <summary>Determines whether two paths denote the same location under the configured platform rules.</summary>
    public bool Equals(string? x, string? y)
    {
        if (x is null || y is null)
        {
            return ReferenceEquals(x, y);
        }
        return string.Equals(Normalize(x), Normalize(y), _comparison);
    }

    /// <summary>Returns a hash code consistent with <see cref="Equals(string?, string?)"/>.</summary>
    public int GetHashCode(string path) => Normalize(path).GetHashCode(_comparison);

    /// <summary>Removes trailing separators while keeping root forms intact ("C:\", "/").</summary>
    private static string TrimTrailingSeparator(string fullPath)
    {
        bool isDriveRoot = fullPath.Length == 3
            && char.IsAsciiLetter(fullPath[0])
            && fullPath[1] == ':'
            && IsSeparator(fullPath[2]);
        if (isDriveRoot || fullPath == "/")
        {
            return fullPath;
        }
        return fullPath.TrimEnd('/', '\\');
    }

    /// <summary>Returns true for both supported separator characters.</summary>
    private static bool IsSeparator(char character) => character is '/' or '\\';
}
