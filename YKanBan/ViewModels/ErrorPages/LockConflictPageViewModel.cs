using System.Globalization;

namespace YKanBan.ViewModels.ErrorPages;

/// <summary>
/// The full-window page shown when the workspace is already locked by another
/// instance. It has no operations.
/// </summary>
public sealed class LockConflictPageViewModel : ViewModelBase {
    /// <summary>
    /// Initializes the page for a locked workspace.
    /// </summary>
    /// <param name="folderPath">The locked workspace folder.</param>
    public LockConflictPageViewModel(string folderPath) {
        FolderPath = folderPath;
        Message = string.Format(
            Resources.Culture ?? CultureInfo.CurrentCulture, Resources.Error_LockConflict_Message, folderPath);
    }

    /// <summary>
    /// Gets the locked workspace folder.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    /// Gets the localized explanatory message.
    /// </summary>
    public string Message { get; }
}
