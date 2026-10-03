using System.Globalization;

namespace YKanBan.ViewModels.ErrorPages;

/// <summary>
/// The full-window page shown when the folder passed on the command line does
/// not exist. It has no operations.
/// </summary>
public sealed class DirectoryMissingPageViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes the page for a missing folder.
    /// </summary>
    /// <param name="folderPath">The folder path that does not exist.</param>
    public DirectoryMissingPageViewModel(string folderPath)
    {
        FolderPath = folderPath;
        Message = string.Format(
            Resources.Culture ?? CultureInfo.CurrentCulture, Resources.Error_DirectoryMissing_Message, folderPath);
    }

    /// <summary>
    /// Gets the folder path that does not exist.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    /// Gets the localized explanatory message.
    /// </summary>
    public string Message { get; }
}
