using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using YKanBan.Models;
using YKanBan.Search;
using YKanBan.Services;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;

namespace YKanBan.ViewModels.Board;

/// <summary>
/// The board tab: a toolbar (search box, add-column and the Display button)
/// over horizontally arranged columns of vertically stacked cards. All
/// persistence goes through the workspace repository; the board reloads from it
/// after each change.
/// </summary>
public sealed partial class BoardViewModel : ViewModelBase {
    private readonly AppServices _services;
    private readonly WorkspaceRepository _repository;

    // The active search result filter, or null when the whole board is shown.
    private IReadOnlyCollection<long>? _matchedCardIds;

    /// <summary>
    /// Initializes the board, loading its current content.
    /// </summary>
    /// <param name="services">The process composition root (configuration access).</param>
    /// <param name="session">The open workspace session.</param>
    public BoardViewModel(AppServices services, WorkspaceSession session) {
        _services = services;
        _repository = session.Repository;
        Display = new DisplaySettingsViewModel(_services.Config, Reload);

        Reload();
    }

    /// <summary>
    /// Gets the workspace repository used for all board data access.
    /// </summary>
    public WorkspaceRepository Repository => _repository;

    /// <summary>
    /// Gets the live application configuration.
    /// </summary>
    public AppConfig Config => _services.Config;

    /// <summary>
    /// Gets the display options (column width, card sort, column sort).
    /// </summary>
    public DisplaySettingsViewModel Display { get; }

    /// <summary>
    /// Gets the columns shown by the board, in display order.
    /// </summary>
    public ObservableCollection<ColumnViewModel> Columns { get; } = [];

    /// <summary>
    /// Gets or sets the search box text. The board does not filter on change;
    /// the search runs only when the search button is used.
    /// </summary>
    [ObservableProperty]
    private string searchText = string.Empty;

    /// <summary>
    /// Gets the rendered column width in pixels, applied globally.
    /// </summary>
    [ObservableProperty]
    private double columnWidth;

    /// <summary>
    /// Gets a value indicating whether a search filter is currently applied.
    /// </summary>
    public bool IsFiltering => _matchedCardIds is not null;

    /// <summary>
    /// Gets the localized filter status text shown as the status icon tooltip.
    /// </summary>
    public string FilterStatusText => IsFiltering ? Resources.Board_FilterActive : Resources.Board_FilterInactive;

    /// <summary>
    /// Gets the status icon opacity: full while filtering, dimmed otherwise.
    /// </summary>
    public double FilterIconOpacity => IsFiltering ? 1.0 : 0.4;

    /// <summary>
    /// Gets a value indicating whether the board has any columns.
    /// </summary>
    public bool HasColumns => Columns.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the board is empty.
    /// </summary>
    public bool HasNoColumns => Columns.Count == 0;

    #region Interactions

    /// <summary>
    /// Gets the interaction that opens the new-column editor.
    /// </summary>
    public Interaction<Unit> AddColumn { get; } = new();

    /// <summary>
    /// Gets the interaction that opens the column editor.
    /// </summary>
    public Interaction<ColumnViewModel> EditColumn { get; } = new();

    /// <summary>
    /// Gets the interaction that confirms and deletes a column.
    /// </summary>
    public Interaction<ColumnViewModel> DeleteColumn { get; } = new();

    /// <summary>
    /// Gets the interaction that opens the new-card editor.
    /// </summary>
    public Interaction<ColumnViewModel> AddCard { get; } = new();

    /// <summary>
    /// Gets the interaction that opens the card editor.
    /// </summary>
    public Interaction<CardViewModel> EditCard { get; } = new();

    /// <summary>
    /// Gets the interaction that confirms and deletes a card.
    /// </summary>
    public Interaction<CardViewModel> DeleteCard { get; } = new();

    /// <summary>
    /// Gets the interaction that opens the move-card dialog.
    /// </summary>
    public Interaction<CardViewModel> MoveCard { get; } = new();

    /// <summary>
    /// Gets the interaction that shows the search error dialog, carrying the English parser detail.
    /// </summary>
    public Interaction<string> ShowSearchError { get; } = new();

    #endregion

    #region Commands

    /// <summary>
    /// Opens the new-column editor.
    /// </summary>
    [RelayCommand]
    private Task AddNewColumnAsync() => AddColumn.Handle(Unit.Value);

    /// <summary>
    /// Runs the current search. A blank query shows everything; an invalid
    /// expression opens the error dialog and keeps the previous results and text.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync() {
        if (string.IsNullOrWhiteSpace(SearchText)) {
            ApplyFilter(null);
            return;
        }

        try {
            SearchNode? query = SearchQueryParser.Parse(SearchText);
            if (query is null) {
                ApplyFilter(null);
                return;
            }

            CompiledSearch compiled = SearchSqlCompiler.Compile(query);
            ApplyFilter(_repository.GetCardIdsMatching(compiled));
        } catch (SearchSyntaxException exception) {
            // The error dialog carries the parser's non-localized English detail.
            await ShowSearchError.Handle(exception.Message);
        }
    }

    /// <summary>
    /// Clears the search box and restores the unfiltered board.
    /// </summary>
    [RelayCommand]
    private void ClearSearch() {
        SearchText = string.Empty;
        ApplyFilter(null);
    }

    /// <summary>
    /// Applies a new result filter (null clears it) and rebuilds the board when it changed.
    /// </summary>
    /// <param name="matchedIds">The new filter, or <see langword="null"/> for no filter.</param>
    private void ApplyFilter(IReadOnlyCollection<long>? matchedIds) {
        if (_matchedCardIds is null && matchedIds is null) {
            return;
        }

        _matchedCardIds = matchedIds;
        OnPropertyChanged(nameof(IsFiltering));
        OnPropertyChanged(nameof(FilterStatusText));
        OnPropertyChanged(nameof(FilterIconOpacity));
        Reload();
    }

    #endregion

    #region Requests from columns and cards

    /// <summary>
    /// Requests the new-card editor for the given column.
    /// </summary>
    /// <param name="column">The target column.</param>
    /// <returns>A task completing after the editor closes.</returns>
    public Task RequestAddCardAsync(ColumnViewModel column) => AddCard.Handle(column);

    /// <summary>
    /// Requests the editor for the given card.
    /// </summary>
    /// <param name="card">The card to edit.</param>
    /// <returns>A task completing after the editor closes.</returns>
    public Task RequestEditCardAsync(CardViewModel card) => EditCard.Handle(card);

    /// <summary>
    /// Requests deletion of the given card.
    /// </summary>
    /// <param name="card">The card to delete.</param>
    /// <returns>A task completing after confirmation.</returns>
    public Task RequestDeleteCardAsync(CardViewModel card) => DeleteCard.Handle(card);

    /// <summary>
    /// Requests moving the given card to another column.
    /// </summary>
    /// <param name="card">The card to move.</param>
    /// <returns>A task completing after the move.</returns>
    public Task RequestMoveCardAsync(CardViewModel card) => MoveCard.Handle(card);

    /// <summary>
    /// Requests the editor for the given column.
    /// </summary>
    /// <param name="column">The column to edit.</param>
    /// <returns>A task completing after the editor closes.</returns>
    public Task RequestEditColumnAsync(ColumnViewModel column) => EditColumn.Handle(column);

    /// <summary>
    /// Requests deletion of the given column.
    /// </summary>
    /// <param name="column">The column to delete.</param>
    /// <returns>A task completing after confirmation.</returns>
    public Task RequestDeleteColumnAsync(ColumnViewModel column) => DeleteColumn.Handle(column);

    #endregion

    #region Loading and ordering

    /// <summary>
    /// Reloads all columns and cards from the repository and rebuilds the board.
    /// </summary>
    public void Reload() {
        IReadOnlyList<ColumnModel> columns = _repository.GetColumns();
        IReadOnlyList<CardModel> cards = _repository.GetCards();

        // An active search hides non-matching cards while every column stays visible.
        if (_matchedCardIds is not null) {
            cards = cards.Where(card => _matchedCardIds.Contains(card.Id)).ToList();
        }

        // Group cards by column once so each column can be built independently.
        Dictionary<long, List<CardModel>> cardsByColumn = cards
            .GroupBy(card => card.ColumnId)
            .ToDictionary(group => group.Key, group => group.ToList());

        IEnumerable<ColumnModel> orderedColumns = SortColumns(columns, _services.Config.Display.ColumnSort);

        Columns.Clear();
        foreach (ColumnModel column in orderedColumns) {
            // Cards are sorted within their column by the selected card order.
            IEnumerable<CardModel> columnCards = cardsByColumn.TryGetValue(column.Id, out List<CardModel>? list)
                ? SortCards(list, _services.Config.Display.CardSort)
                : [];
            Columns.Add(new ColumnViewModel(this, column, columnCards.Select(card => new CardViewModel(this, card))));
        }

        ApplyColumnWidth();
        OnPropertyChanged(nameof(HasColumns));
        OnPropertyChanged(nameof(HasNoColumns));
    }

    /// <summary>
    /// Recomputes the global column width and pushes it to every column.
    /// </summary>
    public void ApplyColumnWidth() {
        ColumnWidth = ColumnWidthPixels(_services.Config.Display.ColumnWidth);
        foreach (ColumnViewModel column in Columns) {
            column.Width = ColumnWidth;
        }
    }

    /// <summary>
    /// Resolves a column width setting to its pixel value.
    /// </summary>
    /// <param name="settings">The configured column width.</param>
    /// <returns>The width in pixels.</returns>
    public static double ColumnWidthPixels(ColumnWidthSettings settings) => settings.Preset switch {
        ColumnWidthPreset.Narrow => 240,
        ColumnWidthPreset.Wide => 480,
        ColumnWidthPreset.Custom => settings.CustomPixels,
        _ => 320,
    };

    /// <summary>
    /// Orders cards by the selected option, tie-breaking by id ascending.
    /// </summary>
    /// <param name="cards">The cards to order.</param>
    /// <param name="option">The selected order.</param>
    /// <returns>The ordered cards.</returns>
    private static IEnumerable<CardModel> SortCards(IEnumerable<CardModel> cards, SortOption option) => option switch {
        SortOption.IdAscending => cards.OrderBy(card => card.Id),
        SortOption.IdDescending => cards.OrderByDescending(card => card.Id),
        SortOption.CreatedAscending => cards.OrderBy(card => card.CreatedAt).ThenBy(card => card.Id),
        SortOption.CreatedDescending => cards.OrderByDescending(card => card.CreatedAt).ThenBy(card => card.Id),
        SortOption.UpdatedAscending => cards.OrderBy(card => card.UpdatedAt).ThenBy(card => card.Id),
        SortOption.UpdatedDescending => cards.OrderByDescending(card => card.UpdatedAt).ThenBy(card => card.Id),
        SortOption.TitleAscending => cards.OrderBy(card => card.Title, StringComparer.Ordinal).ThenBy(card => card.Id),
        SortOption.TitleDescending => cards.OrderByDescending(card => card.Title, StringComparer.Ordinal).ThenBy(card => card.Id),
        _ => cards.OrderBy(card => card.Id),
    };

    /// <summary>
    /// Orders columns by the selected option, tie-breaking by id ascending.
    /// </summary>
    /// <param name="columns">The columns to order.</param>
    /// <param name="option">The selected order.</param>
    /// <returns>The ordered columns.</returns>
    private static IEnumerable<ColumnModel> SortColumns(IEnumerable<ColumnModel> columns, SortOption option) => option switch {
        SortOption.IdAscending => columns.OrderBy(column => column.Id),
        SortOption.IdDescending => columns.OrderByDescending(column => column.Id),
        SortOption.CreatedAscending => columns.OrderBy(column => column.CreatedAt).ThenBy(column => column.Id),
        SortOption.CreatedDescending => columns.OrderByDescending(column => column.CreatedAt).ThenBy(column => column.Id),
        SortOption.UpdatedAscending => columns.OrderBy(column => column.UpdatedAt).ThenBy(column => column.Id),
        SortOption.UpdatedDescending => columns.OrderByDescending(column => column.UpdatedAt).ThenBy(column => column.Id),
        SortOption.TitleAscending => columns.OrderBy(column => column.Title, StringComparer.Ordinal).ThenBy(column => column.Id),
        SortOption.TitleDescending => columns.OrderByDescending(column => column.Title, StringComparer.Ordinal).ThenBy(column => column.Id),
        _ => columns.OrderBy(column => column.Id),
    };

    #endregion
}
