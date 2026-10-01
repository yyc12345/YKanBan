namespace YKanBan.Storage.Workspace;

/// <summary>Well-known file and folder names inside a workspace's .ykanban structure.</summary>
public static class WorkspacePaths
{
    /// <summary>Name of the workspace root folder created inside a managed project.</summary>
    public const string RootFolderName = ".ykanban";

    /// <summary>Name of the SQLite database inside .ykanban.</summary>
    public const string DatabaseFileName = "ykanban.db";

    /// <summary>Name of the exclusive lock file inside .ykanban.</summary>
    public const string LockFileName = "ykanban.lock";

    /// <summary>Returns the .ykanban root path for a workspace folder.</summary>
    public static string Root(string folderPath) => Path.Combine(folderPath, RootFolderName);

    /// <summary>Returns the workspace database path for a workspace folder.</summary>
    public static string Database(string folderPath) => Path.Combine(folderPath, RootFolderName, DatabaseFileName);

    /// <summary>Returns the workspace lock file path for a workspace folder.</summary>
    public static string LockFile(string folderPath) => Path.Combine(folderPath, RootFolderName, LockFileName);
}
