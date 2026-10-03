using System.Globalization;
using CommunityToolkit.Mvvm.Input;

namespace YKanBan.ViewModels.ErrorPages;

/// <summary>
/// The full-window page shown when the folder exists but has no .ykanban
/// structure. It is the only error page with an operation: initializing the
/// workspace.
/// </summary>
public sealed partial class NotInitializedPageViewModel : ViewModelBase
{
    private readonly Interaction<Unit> _initializeWorkspace;

    /// <summary>
    /// Initializes the page for a folder without a workspace.
    /// </summary>
    /// <param name="folderPath">The uninitialized folder path.</param>
    /// <param name="initializeWorkspace">The interaction that performs initialization.</param>
    public NotInitializedPageViewModel(string folderPath, Interaction<Unit> initializeWorkspace)
    {
        FolderPath = folderPath;
        _initializeWorkspace = initializeWorkspace;
        Message = string.Format(
            Resources.Culture ?? CultureInfo.CurrentCulture, Resources.Error_NotInitialized_Message, folderPath);
    }

    /// <summary>
    /// Gets the uninitialized folder path.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    /// Gets the localized explanatory message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Asks the owning view model to initialize this folder as a workspace.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync() => await _initializeWorkspace.Handle(Unit.Value);
}
