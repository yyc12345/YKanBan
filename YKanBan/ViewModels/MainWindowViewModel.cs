using CommunityToolkit.Mvvm.ComponentModel;
using YKanBan.Services;
using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.ViewModels.Pages;

namespace YKanBan.ViewModels;

/// <summary>
/// Root view model: owns the sidebar and the current content page (welcome,
/// an open workspace, or a failure state page).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(AppServices services)
    {
        Services = services;
        Sidebar = new SidebarViewModel(this, services);
        CurrentContent = new WelcomePageViewModel();
    }

    /// <summary>The composition root.</summary>
    public AppServices Services { get; }

    /// <summary>The workspace sidebar.</summary>
    public SidebarViewModel Sidebar { get; }

    /// <summary>The currently displayed content page.</summary>
    [ObservableProperty]
    private object? currentContent;

    /// <summary>Shows the welcome page (no workspace active).</summary>
    public void ShowWelcome()
    {
        CurrentContent = new WelcomePageViewModel();
    }

    /// <summary>
    /// Opens (or switches to) a workspace from the UI flow: a plain folder
    /// prompts for initialization before opening.
    /// </summary>
    public async Task OpenWorkspaceWithInitializePromptAsync(string folderPath)
    {
        string canonical = Services.PathComparison.Normalize(folderPath);

        if (!WorkspaceInitializer.IsWorkspace(canonical))
        {
            if (!Directory.Exists(canonical))
            {
                ShowFailure(Resources.State_DirectoryMissing_Title, Resources.State_DirectoryMissing_Detail);
                return;
            }

            bool confirmed = await Sidebar.ConfirmInitialize.Handle(canonical);
            if (!confirmed)
            {
                return;
            }
            WorkspaceInitializer.Initialize(canonical);
            WorkspacePreset.SeedColumns(canonical);
        }

        TryOpenWorkspaceSession(canonical);
    }

    /// <summary>Opens (or switches to) a workspace without any initialization prompt (IPC entry).</summary>
    public void OpenWorkspaceFromLaunch(string folderPath)
    {
        TryOpenWorkspaceSession(Services.PathComparison.Normalize(folderPath));
    }

    /// <summary>Opens the session through the pool; typed failures map to localized state pages.</summary>
    public void TryOpenWorkspaceSession(string canonicalPath)
    {
        try
        {
            WorkspaceSession session = Services.Pool.Open(canonicalPath);
            CurrentContent = new WorkspacePageViewModel(session);
            Sidebar.RebuildWorkspaceList();
        }
        catch (Exception ex)
        {
            (string title, string detail) = DescribeOpenFailure(ex);
            ShowFailure(title, detail);
        }
    }

    /// <summary>Displays a localized failure state page.</summary>
    private void ShowFailure(string title, string detail)
    {
        CurrentContent = new MessagePageViewModel(title, detail);
    }

    /// <summary>Maps an open failure to localized title/detail text by exception type.</summary>
    private static (string Title, string Detail) DescribeOpenFailure(Exception exception) => exception switch
    {
        WorkspaceLockException => (Resources.State_LockConflict_Title, Resources.State_LockConflict_Detail),
        WorkspaceNotInitializedException => (Resources.State_NotInitialized_Title, Resources.State_NotInitialized_Detail),
        WorkspaceDirectoryMissingException => (Resources.State_DirectoryMissing_Title, Resources.State_DirectoryMissing_Detail),
        SchemaVersionException => (Resources.State_SchemaVersion_Title, Resources.State_SchemaVersion_Detail),
        _ => (Resources.State_Unknown_Title, exception.Message),
    };
}
