using Avalonia.Controls;

using YKanBan.ViewModels.Dialogs;
using YKanBan.ViewModels.Tags;
using YKanBan.Views.Dialogs;

namespace YKanBan.Views.Tags;

public partial class TagsView : UserControl {
    private bool _registered;

    public TagsView() {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);
        if (DataContext is TagsViewModel tags && !_registered) {
            _registered = true;
            tags.AddTag.RegisterHandler(_ => EditTagAsync(tags, null));
            tags.EditTag.RegisterHandler(item => EditTagAsync(tags, item));
            tags.DeleteTag.RegisterHandler(item => DeleteTagAsync(tags, item));
        }
    }

    /// <summary>
    /// Gets the window that owns this view, used as the dialog owner.
    /// </summary>
    private Window? OwningWindow => TopLevel.GetTopLevel(this) as Window;

    /// <summary>
    /// Shows the add/edit tag dialog and refreshes on success.
    /// </summary>
    private async Task EditTagAsync(TagsViewModel tags, TagItemViewModel? item) {
        if (OwningWindow is not Window owner) {
            return;
        }

        var dialog = new TagEditorDialog {
            DataContext = new TagEditorViewModel(tags.Repository, item?.Tag),
        };
        if (await ModalWindow.ShowAsync<bool>(owner, dialog)) {
            tags.RefreshAfterChange();
        }
    }

    /// <summary>
    /// Confirms and deletes a tag, then refreshes.
    /// </summary>
    private async Task DeleteTagAsync(TagsViewModel tags, TagItemViewModel item) {
        if (OwningWindow is not Window owner) {
            return;
        }

        bool confirmed = true;
        if (App.Services?.Config.Confirmations.DeleteTag == true) {
            var dialog = new DeleteTagConfirmDialog {
                DataContext = new DeleteTagConfirmViewModel(item.Name, item.UsageCount),
            };
            confirmed = await ModalWindow.ShowAsync<bool>(owner, dialog);
        }

        if (!confirmed) {
            return;
        }

        tags.Repository.DeleteTag(item.Tag.Id);
        tags.RefreshAfterChange();
    }
}
