using CommunityToolkit.Mvvm.Input;
using YKanBan.Services;
using YKanBan.Storage.Workspace;
using YKanBan.ViewModels.Board;

namespace YKanBan.ViewModels;

/// <summary>
/// The normal workspace view: the top display area (name, path and the export,
/// settings and about buttons) plus the board and tags tabs.
/// </summary>
public sealed partial class WorkspaceViewModel : ViewModelBase
{
    private readonly Interaction<Unit> _showSettings;
    private readonly Interaction<Unit> _showAbout;

    /// <summary>
    /// Initializes the view for an open workspace.
    /// </summary>
    /// <param name="services">The process composition root.</param>
    /// <param name="session">The open workspace session.</param>
    /// <param name="showSettings">The interaction that opens the settings dialog.</param>
    /// <param name="showAbout">The interaction that opens the about dialog.</param>
    public WorkspaceViewModel(
        AppServices services,
        WorkspaceSession session,
        Interaction<Unit> showSettings,
        Interaction<Unit> showAbout)
    {
        Session = session;
        WorkspaceName = new DirectoryInfo(session.FolderPath).Name;
        WorkspacePath = session.FolderPath;
        _showSettings = showSettings;
        _showAbout = showAbout;
        Board = new BoardViewModel(services, session);
    }

    /// <summary>
    /// Gets the open workspace session (lock plus repository).
    /// </summary>
    public WorkspaceSession Session { get; }

    /// <summary>
    /// Gets the workspace display name (the folder name).
    /// </summary>
    public string WorkspaceName { get; }

    /// <summary>
    /// Gets the full workspace folder path.
    /// </summary>
    public string WorkspacePath { get; }

    /// <summary>
    /// Gets the board tab view model.
    /// </summary>
    public BoardViewModel Board { get; }

    /// <summary>
    /// Opens the settings dialog through the registered interaction.
    /// </summary>
    [RelayCommand]
    private async Task OpenSettingsAsync() => await _showSettings.Handle(Unit.Value);

    /// <summary>
    /// Opens the about dialog through the registered interaction.
    /// </summary>
    [RelayCommand]
    private async Task OpenAboutAsync() => await _showAbout.Handle(Unit.Value);

    /// <summary>
    /// Raised by the export button; the export flow is wired up with the export
    /// feature.
    /// </summary>
    [RelayCommand]
    private static void Export()
    {
    }
}
