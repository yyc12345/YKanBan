using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Models;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.ViewModels.Tags;

/// <summary>
/// The tags tab, a tag manager in the style of the GitHub issue labels page:
/// every tag with its color, name, description and usage count, plus creating,
/// editing (rename, recolor, describe) and deleting tags. Unreferenced tags are
/// never cleaned up automatically, so this is where they are removed.
/// </summary>
public sealed partial class TagsViewModel : ViewModelBase
{
    private readonly WorkspaceRepository _repository;
    private readonly AppConfig _config;
    private readonly IDialogService _dialogs;

    /// <summary>
    /// Initializes the tab and loads it.
    /// </summary>
    /// <param name="repository">The workspace repository.</param>
    /// <param name="config">The live application configuration (delete confirmation).</param>
    /// <param name="dialogs">The modal dialog service.</param>
    public TagsViewModel(WorkspaceRepository repository, AppConfig config, IDialogService dialogs)
    {
        _repository = repository;
        _config = config;
        _dialogs = dialogs;
        Refresh();
    }

    /// <summary>
    /// Raised after every tag write, so the board can show the new names and colors.
    /// </summary>
    public event EventHandler? TagsChanged;

    /// <summary>
    /// Gets the tags in id order.
    /// </summary>
    public ObservableCollection<TagRowViewModel> Tags { get; } = [];

    /// <summary>
    /// Gets the localized total, for example "3 tags".
    /// </summary>
    public string Summary => string.Format(Resources.Culture, Resources.Tags_Summary, Tags.Count);

    /// <summary>
    /// Gets whether the workspace has no tags yet.
    /// </summary>
    public bool IsEmpty => Tags.Count == 0;

    /// <summary>
    /// Reloads the tags with their usage counts.
    /// </summary>
    public void Refresh()
    {
        Tags.Clear();
        foreach ((TagModel tag, long usage) in _repository.GetTagsWithUsage())
        {
            Tags.Add(new TagRowViewModel(this, tag, usage));
        }
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Opens the new-tag dialog.
    /// </summary>
    /// <returns>A task completing when the dialog closes.</returns>
    [RelayCommand]
    private async Task AddTagAsync()
    {
        var dialog = new TagEditorDialogViewModel(
            null,
            TagColors.DefaultFor(Tags.Count),
            (name, color, description) => _repository.AddTag(name, color, description));
        if (await _dialogs.ShowAsync(dialog))
        {
            AfterWrite();
        }
    }

    /// <summary>
    /// Opens the edit-tag dialog.
    /// </summary>
    /// <param name="tag">The tag to edit.</param>
    /// <returns>A task completing when the dialog closes.</returns>
    internal async Task EditTagAsync(TagModel tag)
    {
        var dialog = new TagEditorDialogViewModel(
            tag,
            tag.Color,
            (name, color, description) => _repository.UpdateTag(tag.Id, name, color, description));
        if (await _dialogs.ShowAsync(dialog))
        {
            AfterWrite();
        }
    }

    /// <summary>
    /// Deletes a tag (removing it from its cards), asking first when that
    /// confirmation is enabled.
    /// </summary>
    /// <param name="tag">The tag to delete.</param>
    /// <param name="usageCount">The number of cards carrying it.</param>
    /// <returns>A task completing when the tag is deleted or the deletion was declined.</returns>
    internal async Task DeleteTagAsync(TagModel tag, long usageCount)
    {
        if (_config.Confirmations.DeleteTag)
        {
            string message = string.Format(Resources.Culture, Resources.Confirm_DeleteTag, tag.Name, usageCount);
            if (!await _dialogs.ConfirmAsync(message, Resources.Common_Delete))
            {
                return;
            }
        }

        try
        {
            _repository.DeleteTag(tag.Id);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            await _dialogs.ShowMessageAsync(
                Resources.Error_Operation_Title, Resources.Error_Operation_Message, exception.Message);
        }
        AfterWrite();
    }

    /// <summary>
    /// Reloads the list and tells the board that tags changed.
    /// </summary>
    private void AfterWrite()
    {
        Refresh();
        TagsChanged?.Invoke(this, EventArgs.Empty);
    }
}
