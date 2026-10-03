using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Models;
using YKanBan.Search;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;
using YKanBan.ViewModels.Dialogs;

namespace YKanBan.ViewModels.Board;

/// <summary>
/// The board tab: the toolbar (new column, column width, card sort) and the
/// columns laid out left to right, each listing its cards top to bottom.
///
/// Every write is followed by <see cref="Refresh"/>, which re-queries with the
/// most recently successful search expression (<see cref="ActiveFilter"/>), not
/// with whatever the search box currently holds: cards that no longer match
/// disappear, new cards that do not match are not shown, and empty columns stay.
/// </summary>
public sealed partial class BoardViewModel : ViewModelBase
{
    private readonly WorkspaceRepository _repository;
    private readonly AppConfig _config;
    private readonly IDialogService _dialogs;

    /// <summary>
    /// Initializes the board and loads it.
    /// </summary>
    /// <param name="repository">The workspace repository.</param>
    /// <param name="config">The live application configuration (sort, column width, confirmations).</param>
    /// <param name="dialogs">The modal dialog service.</param>
    public BoardViewModel(WorkspaceRepository repository, AppConfig config, IDialogService dialogs)
    {
        _repository = repository;
        _config = config;
        _dialogs = dialogs;

        SortOptions =
        [
            new OptionItem(CardSortOption.Id, Resources.Sort_Id),
            new OptionItem(CardSortOption.UpdatedAt, Resources.Sort_UpdatedAt),
            new OptionItem(CardSortOption.Title, Resources.Sort_Title),
        ];
        selectedSort = SortOptions.FirstOrDefault(option => (CardSortOption)option.Value == config.Sort.Card)
            ?? SortOptions[0];
        columnWidth = ColumnWidths.Resolve(config.ColumnWidth);

        Refresh();
    }

    /// <summary>
    /// Gets the columns in id order.
    /// </summary>
    public ObservableCollection<ColumnViewModel> Columns { get; } = [];

    /// <summary>
    /// Gets the card sort options shown in the combobox.
    /// </summary>
    public IReadOnlyList<OptionItem> SortOptions { get; }

    /// <summary>
    /// Gets the column models in id order, as of the last refresh.
    /// </summary>
    public IReadOnlyList<ColumnModel> ColumnModels { get; private set; } = [];

    /// <summary>
    /// Gets or sets the most recently successful search; <see langword="null"/> shows every card.
    /// </summary>
    public CompiledSearch? ActiveFilter { get; set; }

    /// <summary>
    /// Gets or sets the selected card sort option; changing it is persisted and re-sorts the board.
    /// </summary>
    [ObservableProperty]
    private OptionItem selectedSort;

    /// <summary>
    /// Gets the effective column width in pixels.
    /// </summary>
    [ObservableProperty]
    private int columnWidth;

    /// <summary>
    /// Reloads the columns and the cards matching <see cref="ActiveFilter"/>.
    /// </summary>
    public void Refresh()
    {
        IReadOnlyList<ColumnModel> columns = _repository.GetColumns();
        ILookup<long, CardModel> cardsByColumn = _repository.GetCards(ActiveFilter).ToLookup(card => card.ColumnId);
        var sort = (CardSortOption)SelectedSort.Value;

        ColumnModels = columns;
        Columns.Clear();
        foreach (ColumnModel column in columns)
        {
            Columns.Add(new ColumnViewModel(this, column, CardOrdering.Sort(cardsByColumn[column.Id], sort)));
        }
    }

    /// <summary>
    /// Opens the new-column dialog.
    /// </summary>
    /// <returns>A task completing when the dialog closes.</returns>
    [RelayCommand]
    private async Task AddColumnAsync()
    {
        var dialog = new ColumnEditorDialogViewModel(null, (title, description) => _repository.AddColumn(title, description));
        if (await _dialogs.ShowAsync(dialog))
        {
            Refresh();
        }
    }

    /// <summary>
    /// Opens the column width dialog and applies the choice.
    /// </summary>
    /// <returns>A task completing when the dialog closes.</returns>
    [RelayCommand]
    private async Task AdjustColumnWidthAsync()
    {
        if (await _dialogs.ShowAsync(new ColumnWidthDialogViewModel(_config.ColumnWidth)))
        {
            ColumnWidth = ColumnWidths.Resolve(_config.ColumnWidth);
        }
    }

    /// <summary>
    /// Opens the edit-column dialog.
    /// </summary>
    /// <param name="column">The column to edit.</param>
    /// <returns>A task completing when the dialog closes.</returns>
    internal async Task EditColumnAsync(ColumnModel column)
    {
        var dialog = new ColumnEditorDialogViewModel(
            column, (title, description) => _repository.UpdateColumn(column.Id, title, description));
        if (await _dialogs.ShowAsync(dialog))
        {
            Refresh();
        }
    }

    /// <summary>
    /// Deletes a column with its cards, asking first when that confirmation is enabled.
    /// </summary>
    /// <param name="column">The column to delete.</param>
    /// <returns>A task completing when the column is deleted or the deletion was declined.</returns>
    internal async Task DeleteColumnAsync(ColumnModel column)
    {
        await RunWriteAsync(async () =>
        {
            if (_config.Confirmations.DeleteColumn)
            {
                long count = _repository.CountCards(column.Id);
                string message = string.Format(Resources.Culture, Resources.Confirm_DeleteColumn, column.Title, count);
                if (!await _dialogs.ConfirmAsync(message, Resources.Common_Delete))
                {
                    return false;
                }
            }
            _repository.DeleteColumn(column.Id);
            return true;
        });
    }

    /// <summary>
    /// Opens the card editor for a new card in a column.
    /// </summary>
    /// <param name="columnId">The column receiving the card.</param>
    /// <returns>A task completing when the editor closes.</returns>
    internal async Task AddCardAsync(long columnId)
    {
        var editor = CardEditorDialogViewModel.ForNewCard(
            _repository, _dialogs, columnId, _config.Confirmations.DiscardEdit);
        await _dialogs.ShowAsync(editor);

        // Refresh even after a cancel: the nested picker may have created tags.
        Refresh();
    }

    /// <summary>
    /// Opens the card editor for an existing card, reloaded from the database.
    /// </summary>
    /// <param name="cardId">The card to edit.</param>
    /// <returns>A task completing when the editor closes.</returns>
    internal async Task EditCardAsync(long cardId)
    {
        CardModel? card = _repository.GetCard(cardId);
        if (card is null)
        {
            Refresh();
            return;
        }

        var editor = CardEditorDialogViewModel.ForExistingCard(
            _repository, _dialogs, card, _config.Confirmations.DiscardEdit);
        await _dialogs.ShowAsync(editor);
        Refresh();
    }

    /// <summary>
    /// Deletes a card, asking first when that confirmation is enabled.
    /// </summary>
    /// <param name="card">The card to delete.</param>
    /// <returns>A task completing when the card is deleted or the deletion was declined.</returns>
    internal async Task DeleteCardAsync(CardModel card)
    {
        await RunWriteAsync(async () =>
        {
            if (_config.Confirmations.DeleteCard)
            {
                string message = string.Format(Resources.Culture, Resources.Confirm_DeleteCard, CardViewModel.FormatHeader(card));
                if (!await _dialogs.ConfirmAsync(message, Resources.Common_Delete))
                {
                    return false;
                }
            }
            _repository.DeleteCard(card.Id);
            return true;
        });
    }

    /// <summary>
    /// Moves a card to another column.
    /// </summary>
    /// <param name="cardId">The card to move.</param>
    /// <param name="columnId">The target column.</param>
    /// <returns>A task completing when the card is moved.</returns>
    internal Task MoveCardAsync(long cardId, long columnId) =>
        RunWriteAsync(() =>
        {
            _repository.MoveCard(cardId, columnId);
            return Task.FromResult(true);
        });

    /// <summary>
    /// Runs an immediate write and refreshes after it; an unexpected failure is
    /// reported in a message dialog and the board is refreshed to the stored state.
    /// </summary>
    /// <param name="write">The write; returns <see langword="false"/> when the user declined it.</param>
    /// <returns>A task completing after the refresh.</returns>
    private async Task RunWriteAsync(Func<Task<bool>> write)
    {
        try
        {
            if (!await write())
            {
                return;
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            await _dialogs.ShowMessageAsync(
                Resources.Error_Operation_Title, Resources.Error_Operation_Message, exception.Message);
        }
        Refresh();
    }

    /// <summary>
    /// Persists the sort option and re-sorts the board.
    /// </summary>
    /// <param name="value">The new sort option.</param>
    partial void OnSelectedSortChanged(OptionItem value)
    {
        _config.Sort.Card = (CardSortOption)value.Value;
        Refresh();
    }
}
