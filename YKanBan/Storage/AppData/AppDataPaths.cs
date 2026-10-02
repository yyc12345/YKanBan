namespace YKanBan.Storage.AppData;

/// <summary>Well-known locations under the per-user application data folder.</summary>
public static class AppDataPaths
{
    /// <summary>The per-user YKanBan application data folder.</summary>
    public static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YKanBan");

    /// <summary>Full path of app.json.</summary>
    public static string AppConfigJsonPath => Path.Combine(Directory, "app.json");

    /// <summary>Full path of app.db.</summary>
    public static string AppDatabasePath => Path.Combine(Directory, "app.db");
}
