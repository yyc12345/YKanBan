using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Export;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;
using YKanBan.ViewModels.Board;
using YKanBan.ViewModels.Dialogs;
using YKanBan.ViewModels.Tags;

namespace YKanBan.ViewModels;

/// <summary>
/// The normal workspace view: the top display area (name, path and the export,
/// settings and about buttons) plus the board and tags tabs. A write on either
/// tab refreshes the other: tag edits change the board's badges and filter
/// matches, card edits change the tags' usage counts.
/// </summary>
public sealed partial class WorkspaceViewModel : ViewModelBase
{
    private readonly Interaction<Unit> _showSettings;
    private readonly Interaction<Unit> _showAbout;
    private readonly IDialogService _dialogs;

    /// <summary>
    /// Initializes the view for an open workspace.
    /// </summary>
    /// <param name="session">The open workspace session.</param>
    /// <param name="showSettings">The interaction that opens the settings dialog.</param>
    /// <param name="showAbout">The interaction that opens the about dialog.</param>
    /// <param name="config">The live application configuration.</param>
    /// <param name="dialogs">The modal dialog service.</param>
    public WorkspaceViewModel(
        WorkspaceSession session,
        Interaction<Unit> showSettings,
        Interaction<Unit> showAbout,
        AppConfig config,
        IDialogService dialogs)
    {
        Session = session;
        WorkspaceName = new DirectoryInfo(session.FolderPath).Name;
        WorkspacePath = session.FolderPath;
        _showSettings = showSettings;
        _showAbout = showAbout;
        _dialogs = dialogs;
        Board = new BoardViewModel(session.Repository, config, dialogs);
        Tags = new TagsViewModel(session.Repository, config, dialogs);
        Board.DataWritten += async (_, _) => await Tags.RefreshOrReportAsync();
        Tags.TagsChanged += async (_, _) => await Board.RefreshOrReportAsync();
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
    /// Gets the board tab.
    /// </summary>
    public BoardViewModel Board { get; }

    /// <summary>
    /// Gets the tags tab.
    /// </summary>
    public TagsViewModel Tags { get; }

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
    /// Exports the whole workspace to a Markdown file chosen in the system save
    /// dialog. The export ignores the board's search filter and card sort; the
    /// default file name and all template text follow the UI language.
    /// </summary>
    /// <returns>A task completing when the file is written or the export was cancelled.</returns>
    [RelayCommand]
    private async Task ExportAsync()
    {
        DateTimeOffset exportedAt = DateTimeOffset.Now;
        string? path = await _dialogs.PickSaveFilePathAsync(MarkdownExporter.BuildFileName(WorkspaceName, exportedAt));
        if (path is null)
        {
            return;
        }

        try
        {
            WorkspaceRepository repository = Session.Repository;
            string markdown = MarkdownExporter.Export(
                WorkspaceName, repository.GetColumns(), repository.GetCards(), repository.GetTagsWithUsage(), exportedAt);
            await File.WriteAllTextAsync(path, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            await _dialogs.ShowMessageAsync(Resources.Export_Failed_Title, Resources.Export_Failed_Message, exception.Message);
        }
    }
}
