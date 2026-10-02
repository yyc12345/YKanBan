using YKanBan.Storage.Workspace;

namespace YKanBan.ViewModels.Pages;

/// <summary>
/// An open workspace page. The board, tags and search views mount here in
/// the later UI milestones; the frame shows the workspace identity for now.
/// </summary>
public sealed class WorkspacePageViewModel : ViewModelBase
{
    public WorkspacePageViewModel(WorkspaceSession session)
    {
        FullPath = session.FolderPath;
        Name = Path.GetFileName(session.FolderPath);
    }

    /// <summary>Canonical workspace folder path.</summary>
    public string FullPath { get; }

    /// <summary>Display name: the folder name.</summary>
    public string Name { get; }
}
