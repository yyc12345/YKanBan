using System.Diagnostics;

using CommunityToolkit.Mvvm.ComponentModel;

using YKanBan.Launching;
using YKanBan.Services;
using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.ViewModels.ErrorPages;

namespace YKanBan.ViewModels;

/// <summary>
/// The main window view model. It resolves the launch arguments into either the
/// normal workspace view or one of the full-window error pages, and owns the
/// interactions that the view fulfils by opening dialogs.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase {
    private readonly AppServices _services;

    /// <summary>
    /// Initializes the view model and resolves the workspace state.
    /// </summary>
    /// <param name="services">The process composition root.</param>
    /// <param name="arguments">The command-line arguments (excluding the executable).</param>
    public MainWindowViewModel(AppServices services, IReadOnlyList<string> arguments) {
        _services = services;
        ShowSettings = new Interaction<Unit>();
        ShowAbout = new Interaction<Unit>();
        InitializeWorkspace = new Interaction<Unit>();
        InitializeWorkspace.RegisterHandler(_ => InitializeCurrentWorkspaceAsync());
        Resolve(arguments);
    }

    /// <summary>
    /// Gets the interaction that opens the settings dialog.
    /// </summary>
    public Interaction<Unit> ShowSettings { get; }

    /// <summary>
    /// Gets the interaction that opens the about dialog.
    /// </summary>
    public Interaction<Unit> ShowAbout { get; }

    /// <summary>
    /// Gets the interaction that initializes the not-initialized workspace.
    /// </summary>
    public Interaction<Unit> InitializeWorkspace { get; }

    /// <summary>
    /// Gets or sets the window title.
    /// </summary>
    [ObservableProperty]
    private string title = Resources.App_Name;

    /// <summary>
    /// Gets or sets the current content: the workspace view or an error page.
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? content;

    /// <summary>
    /// Resolves the launch arguments into a content view model.
    /// </summary>
    /// <param name="arguments">The command-line arguments.</param>
    private void Resolve(IReadOnlyList<string> arguments) {
        if (!LaunchArguments.TryParse(arguments, out string? folderPath)) {
            Content = new ArgumentErrorPageViewModel();
            return;
        }

        if (!Directory.Exists(folderPath)) {
            Content = new DirectoryMissingPageViewModel(folderPath);
            return;
        }

        try {
            OpenWorkspace(folderPath);
        } catch (WorkspaceNotInitializedException) {
            Content = new NotInitializedPageViewModel(folderPath, InitializeWorkspace);
        } catch (WorkspaceLockException) {
            Content = new LockConflictPageViewModel(folderPath);
        } catch (Exception exception) {
            // Newer schema, damaged database, I/O or permission errors, and anything unrecognized.
            Debug.WriteLine(exception);
            Content = new OpenFailedPageViewModel(folderPath, exception);
        }
    }

    /// <summary>
    /// Opens the workspace and switches the content to the normal view.
    /// </summary>
    /// <param name="folderPath">The workspace folder to open.</param>
    private void OpenWorkspace(string folderPath) => OpenWorkspace(folderPath, _services.OpenWorkspace(folderPath));

    /// <summary>
    /// Switches the content to the normal view of an already opened workspace.
    /// </summary>
    /// <param name="folderPath">The workspace folder.</param>
    /// <param name="session">The opened workspace session.</param>
    private void OpenWorkspace(string folderPath, WorkspaceSession session) {
        try {
            Content = new WorkspaceViewModel(_services, session, ShowSettings, ShowAbout);
        } catch {
            // The first board read failed: the caller shows an error page, which holds no workspace.
            _services.CloseWorkspace();
            throw;
        }

        Title = $"{new DirectoryInfo(folderPath).Name} - {Resources.App_Name}";
    }

    /// <summary>
    /// Initializes the folder shown by the not-initialized page, adds the preset
    /// columns and opens the resulting workspace.
    /// </summary>
    /// <returns>A completed task once initialization has been attempted.</returns>
    private Task InitializeCurrentWorkspaceAsync() {
        if (Content is not NotInitializedPageViewModel page) {
            return Task.CompletedTask;
        }

        try {
            OpenWorkspace(page.FolderPath, _services.InitializeWorkspace(page.FolderPath));
        } catch (Exception exception) when (exception is YKanBanException or IOException or UnauthorizedAccessException) {
            // Initialization failed; the page remains so the user can retry.
            Debug.WriteLine(exception);
        }

        return Task.CompletedTask;
    }
}
