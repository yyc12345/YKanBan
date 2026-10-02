namespace YKanBan.Storage.Workspace;

/// <summary>
/// Well-known file and folder names inside a workspace's .ykanban structure,
/// plus helpers that combine them with a workspace folder path.
/// </summary>
public static class WorkspacePaths
{
    /// <summary>
    /// Name of the workspace root folder created inside a managed project.
    /// </summary>
    public const string RootFolderName = ".ykanban";

    /// <summary>
    /// Name of the SQLite database inside .ykanban.
    /// </summary>
    public const string DatabaseFileName = "ykanban.db";

    /// <summary>
    /// Name of the exclusive lock file inside .ykanban.
    /// </summary>
    public const string LockFileName = "ykanban.lock";

    /// <summary>
    /// Returns the .ykanban root path for a workspace folder.
    /// </summary>
    /// <param name="folderPath">The workspace folder path.</param>
    /// <returns>The path of the .ykanban folder.</returns>
    public static string Root(string folderPath) => Path.Combine(folderPath, RootFolderName);

    /// <summary>
    /// Returns the workspace database path for a workspace folder.
    /// </summary>
    /// <param name="folderPath">The workspace folder path.</param>
    /// <returns>The path of ykanban.db.</returns>
    public static string Database(string folderPath) => Path.Combine(Root(folderPath), DatabaseFileName);

    /// <summary>
    /// Returns the workspace lock file path for a workspace folder.
    /// </summary>
    /// <param name="folderPath">The workspace folder path.</param>
    /// <returns>The path of ykanban.lock.</returns>
    public static string LockFile(string folderPath) => Path.Combine(Root(folderPath), LockFileName);
}
