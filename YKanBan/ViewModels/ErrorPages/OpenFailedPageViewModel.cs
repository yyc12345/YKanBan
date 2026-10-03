using System.Globalization;
using Microsoft.Data.Sqlite;
using YKanBan.Storage;

namespace YKanBan.ViewModels.ErrorPages;

/// <summary>
/// The full-window page shown when a workspace cannot be opened or initialized:
/// a schema newer than this build, a missing, unopenable or damaged database, an I/O or
/// permission error, or any unrecognized failure. It shows a localized message
/// chosen by exception type plus the read-only, copyable English exception
/// details, and has no operations.
/// </summary>
public sealed class OpenFailedPageViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes the page for a failed open or initialization.
    /// </summary>
    /// <param name="folderPath">The workspace folder.</param>
    /// <param name="exception">The failure.</param>
    public OpenFailedPageViewModel(string folderPath, Exception exception)
    {
        FolderPath = folderPath;
        Message = BuildMessage(folderPath, exception);

        // Exception texts are English: ours by convention, .NET and SQLite ones by design.
        Details = exception.ToString();
    }

    /// <summary>
    /// Gets the workspace folder.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    /// Gets the localized explanatory message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the English exception details for diagnosis.
    /// </summary>
    public string Details { get; }

    /// <summary>
    /// Maps the exception type to its localized message; unrecognized types get the generic one.
    /// </summary>
    /// <param name="folderPath">The workspace folder.</param>
    /// <param name="exception">The failure.</param>
    /// <returns>The localized message.</returns>
    public static string BuildMessage(string folderPath, Exception exception)
    {
        CultureInfo culture = Resources.Culture ?? CultureInfo.CurrentCulture;
        return exception switch
        {
            SchemaVersionException schema => string.Format(
                culture, Resources.Error_OpenFailed_SchemaTooNew, folderPath, schema.StoredVersion, schema.SupportedVersion),
            WorkspaceDatabaseMissingException => string.Format(culture, Resources.Error_OpenFailed_DatabaseMissing, folderPath),
            SqliteException => string.Format(culture, Resources.Error_OpenFailed_Database, folderPath),
            UnauthorizedAccessException => string.Format(culture, Resources.Error_OpenFailed_AccessDenied, folderPath),
            IOException => string.Format(culture, Resources.Error_OpenFailed_Io, folderPath),
            _ => string.Format(culture, Resources.Error_OpenFailed_Generic, folderPath),
        };
    }
}
