using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using YKanBan.Models;
using YKanBan.ViewModels;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.Views.Dialogs;

public partial class CardEditorDialog : Window
{
    private bool _allowClose;

    public CardEditorDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is CardEditorViewModel viewModel)
        {
            viewModel.ShowTagPicker.RegisterHandler(ShowTagPickerAsync);
        }
    }

    /// <summary>Shows the nested tag picker, owned by this dialog.</summary>
    private async Task<TagModel?> ShowTagPickerAsync(Unit input)
    {
        if (DataContext is not CardEditorViewModel viewModel)
        {
            return null;
        }

        var dialog = new TagPickerDialog
        {
            DataContext = new TagPickerViewModel(
                viewModel.Repository,
                viewModel.AssignedTags.Select(row => row.Tag.Id).ToList()),
        };
        return await dialog.ShowDialog<TagModel?>(this);
    }

    /// <summary>Writes the card and closes when accepted.</summary>
    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CardEditorViewModel viewModel && viewModel.TryAccept())
        {
            _allowClose = true;
            Close(true);
        }
    }

    /// <summary>Requests a close, confirming the discard first when needed.</summary>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => _ = RequestCloseAsync();

    /// <summary>Intercepts the window close button to run the discard confirmation.</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_allowClose)
        {
            return;
        }

        if (HasUnsavedChanges && DiscardConfirmEnabled)
        {
            e.Cancel = true;
            Dispatcher.UIThread.Post(() => _ = RequestCloseAsync());
        }
        else
        {
            _allowClose = true;
        }
    }

    /// <summary>Confirms the discard and closes, or stays open when declined.</summary>
    private async Task RequestCloseAsync()
    {
        if (HasUnsavedChanges && DiscardConfirmEnabled)
        {
            var dialog = new DiscardEditConfirmDialog { DataContext = new DiscardEditConfirmViewModel() };
            if (!await dialog.ShowDialog<bool>(this))
            {
                return;
            }
        }

        _allowClose = true;
        Close(false);
    }

    /// <summary>Gets whether the editor has unsaved changes.</summary>
    private bool HasUnsavedChanges => DataContext is CardEditorViewModel viewModel && viewModel.HasChanges;

    /// <summary>Gets whether the discard confirmation setting is enabled.</summary>
    private static bool DiscardConfirmEnabled => App.Services?.Config.Confirmations.DiscardEdit == true;
}
