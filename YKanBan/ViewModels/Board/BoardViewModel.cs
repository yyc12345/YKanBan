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
/// The board tab: the toolbar (search bar, new column, column width, card sort)
/// and the columns laid out left to right, each listing its cards top to bottom.
///
/// The search runs only on an explicit search (button or Enter), never while
/// typing. An invalid expression is reported in a message dialog and changes
/// nothing: the box keeps its text and the board keeps the previous result.
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
    /// Raised after every board write (the refresh included), so the tags tab
    /// can update its usage counts.
    /// </summary>
    public event EventHandler? DataWritten;

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
    /// Gets or sets the search box text; it only takes effect on <see cref="SearchCommand"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    private string searchText = "";

    /// <summary>
    /// Gets whether the search box holds text (shows the clear button).
    /// </summary>
    public bool HasSearchText => SearchText.Length > 0;

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
    /// <exception cref="Exception">Reading the workspace failed; the board is left unchanged.</exception>
    public void Refresh() => Load(ActiveFilter);

    /// <summary>
    /// Reloads like <see cref="Refresh"/>, but reports a read failure in a
    /// message dialog instead of throwing.
    /// </summary>
    /// <returns><see langword="true"/> when the board was reloaded.</returns>
    public Task<bool> RefreshOrReportAsync() => TryLoadAsync(ActiveFilter);

    /// <summary>
    /// Reloads the board with the given filter; the columns are only replaced
    /// once both queries have succeeded.
    /// </summary>
    /// <param name="filter">The search to apply; <see langword="null"/> shows every card.</param>
    private void Load(CompiledSearch? filter)
    {
        IReadOnlyList<ColumnModel> columns = _repository.GetColumns();
        ILookup<long, CardModel> cardsByColumn = _repository.GetCards(filter).ToLookup(card => card.ColumnId);
        var sort = (CardSortOption)SelectedSort.Value;

        ColumnModels = columns;
        Columns.Clear();
        foreach (ColumnModel column in columns)
        {
            Columns.Add(new ColumnViewModel(this, column, CardOrdering.Sort(cardsByColumn[column.Id], sort)));
        }
    }

    /// <summary>
    /// Reloads the board with the given filter and reports a read failure in a message dialog.
    /// </summary>
    /// <param name="filter">The search to apply; <see langword="null"/> shows every card.</param>
    /// <returns><see langword="true"/> when the board was reloaded.</returns>
    private async Task<bool> TryLoadAsync(CompiledSearch? filter)
    {
        try
        {
            Load(filter);
            return true;
        }
        catch (Exception exception)
        {
            await _dialogs.ShowReadFailedAsync(exception);
            return false;
        }
    }

    /// <summary>
    /// Parses and runs the search box text; blank text shows every card. An
    /// invalid expression only shows the error dialog. The expression becomes
    /// <see cref="ActiveFilter"/> only once its query has succeeded, so a failed
    /// search leaves the previous result and later refreshes untouched.
    /// </summary>
    /// <returns>A task completing after the refresh or the error dialog.</returns>
    [RelayCommand]
    private async Task SearchAsync()
    {
        CompiledSearch? filter;
        try
        {
            SearchNode? query = SearchQueryParser.Parse(SearchText);
            filter = query is null ? null : SearchSqlCompiler.Compile(query);
        }
        catch (SearchSyntaxException exception)
        {
            await _dialogs.ShowMessageAsync(
                Resources.Search_Error_Title,
                string.Format(Resources.Culture, Resources.Search_Error_Message, exception.Message));
            return;
        }

        if (await TryLoadAsync(filter))
        {
            ActiveFilter = filter;
        }
    }

    /// <summary>
    /// Empties the search box and shows every card again.
    /// </summary>
    /// <returns>A task completing after the refresh.</returns>
    [RelayCommand]
    private async Task ClearSearchAsync()
    {
        SearchText = "";
        if (await TryLoadAsync(null))
        {
            ActiveFilter = null;
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
            await RefreshAfterWriteAsync();
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
            await RefreshAfterWriteAsync();
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
        await RefreshAfterWriteAsync();
    }

    /// <summary>
    /// Opens the card editor for an existing card, reloaded from the database.
    /// </summary>
    /// <param name="cardId">The card to edit.</param>
    /// <returns>A task completing when the editor closes.</returns>
    internal async Task EditCardAsync(long cardId)
    {
        CardModel? card;
        try
        {
            card = _repository.GetCard(cardId);
        }
        catch (Exception exception)
        {
            await _dialogs.ShowReadFailedAsync(exception);
            return;
        }

        if (card is null)
        {
            await RefreshOrReportAsync();
            return;
        }

        var editor = CardEditorDialogViewModel.ForExistingCard(
            _repository, _dialogs, card, _config.Confirmations.DiscardEdit);
        await _dialogs.ShowAsync(editor);
        await RefreshAfterWriteAsync();
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
        await RefreshAfterWriteAsync();
    }

    /// <summary>
    /// Refreshes after a write and announces it; a failed refresh is reported
    /// and not announced, so a broken workspace yields one read-failure dialog.
    /// </summary>
    /// <returns>A task completing after the refresh.</returns>
    private async Task RefreshAfterWriteAsync()
    {
        if (await RefreshOrReportAsync())
        {
            DataWritten?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Persists the sort option and re-sorts the board.
    /// </summary>
    /// <param name="value">The new sort option.</param>
    partial void OnSelectedSortChanged(OptionItem value)
    {
        _config.Sort.Card = (CardSortOption)value.Value;

        // A property setter cannot await; the refresh reports its own failures.
        _ = RefreshOrReportAsync();
    }
}
