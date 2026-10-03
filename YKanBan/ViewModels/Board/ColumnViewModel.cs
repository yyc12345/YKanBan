using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YKanBan.Models;

namespace YKanBan.ViewModels.Board;

/// <summary>
/// One board column: a header with its title, card count and actions, plus the
/// vertically stacked cards.
/// </summary>
public sealed partial class ColumnViewModel : ViewModelBase
{
    private readonly BoardViewModel _board;

    /// <summary>
    /// Initializes the column view from its model and cards.
    /// </summary>
    /// <param name="board">The owning board view model.</param>
    /// <param name="column">The column model.</param>
    /// <param name="cards">The already sorted cards belonging to this column.</param>
    public ColumnViewModel(BoardViewModel board, ColumnModel column, IEnumerable<CardViewModel> cards)
    {
        _board = board;
        Model = column;
        Cards = new ObservableCollection<CardViewModel>(cards);
        width = board.ColumnWidth;
    }

    /// <summary>
    /// Gets the underlying column model.
    /// </summary>
    public ColumnModel Model { get; }

    /// <summary>
    /// Gets the column id.
    /// </summary>
    public long Id => Model.Id;

    /// <summary>
    /// Gets the column title.
    /// </summary>
    public string Title => Model.Title;

    /// <summary>
    /// Gets the column description.
    /// </summary>
    public string Description => Model.Description;

    /// <summary>
    /// Gets the cards in this column, already in display order.
    /// </summary>
    public ObservableCollection<CardViewModel> Cards { get; }

    /// <summary>
    /// Gets the number of cards in this column.
    /// </summary>
    public int CardCount => Cards.Count;

    /// <summary>
    /// Gets the tooltip text: the column description when present, otherwise
    /// <see langword="null"/> so no tooltip is shown.
    /// </summary>
    public string? ToolTipText => Model.Description.Length > 0 ? Model.Description : null;

    /// <summary>
    /// Gets or sets the rendered column width in pixels, applied globally.
    /// </summary>
    [ObservableProperty]
    private double width;

    /// <summary>
    /// Opens the editor for a new card in this column.
    /// </summary>
    [RelayCommand]
    private void AddCard() => _ = _board.RequestAddCardAsync(this);

    /// <summary>
    /// Opens the editor for this column.
    /// </summary>
    [RelayCommand]
    private void Edit() => _ = _board.RequestEditColumnAsync(this);

    /// <summary>
    /// Asks the board to delete this column.
    /// </summary>
    [RelayCommand]
    private void Delete() => _ = _board.RequestDeleteColumnAsync(this);
}
