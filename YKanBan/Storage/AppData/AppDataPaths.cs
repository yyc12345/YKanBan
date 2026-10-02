namespace YKanBan.Storage.AppData;

/// <summary>
/// Well-known locations under the per-user application data folder
/// (%APPDATA%/YKanBan on Windows and the equivalent directory on other
/// platforms).
/// </summary>
public static class AppDataPaths
{
    /// <summary>
    /// Gets the per-user YKanBan application data folder.
    /// </summary>
    public static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YKanBan");

    /// <summary>
    /// Gets the full path of app.json.
    /// </summary>
    public static string AppConfigJsonPath => Path.Combine(Directory, "app.json");
}
