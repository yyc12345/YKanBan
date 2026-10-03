using CommunityToolkit.Mvvm.Input;
using YKanBan.Models;

namespace YKanBan.ViewModels.Board;

/// <summary>
/// One board column: its header (title, card count, description, the new-card
/// button and the edit/delete menu) and its virtualized card list.
/// </summary>
public sealed partial class ColumnViewModel : ViewModelBase
{
    private readonly BoardViewModel _board;

    /// <summary>
    /// Initializes the column.
    /// </summary>
    /// <param name="board">The owning board.</param>
    /// <param name="column">The column shown.</param>
    /// <param name="cards">The column's visible cards, in display order.</param>
    public ColumnViewModel(BoardViewModel board, ColumnModel column, IReadOnlyList<CardModel> cards)
    {
        _board = board;
        Column = column;
        Cards = cards.Select(card => new CardViewModel(board, card)).ToList();
    }

    /// <summary>
    /// Gets the column shown.
    /// </summary>
    public ColumnModel Column { get; }

    /// <summary>
    /// Gets the column title.
    /// </summary>
    public string Title => Column.Title;

    /// <summary>
    /// Gets the column description.
    /// </summary>
    public string Description => Column.Description;

    /// <summary>
    /// Gets whether the column has a description to show.
    /// </summary>
    public bool HasDescription => Column.Description.Length > 0;

    /// <summary>
    /// Gets the number of visible cards.
    /// </summary>
    public int CardCount => Cards.Count;

    /// <summary>
    /// Gets the visible cards in display order.
    /// </summary>
    public IReadOnlyList<CardViewModel> Cards { get; }

    /// <summary>
    /// Opens the card editor for a new card in this column.
    /// </summary>
    /// <returns>A task completing when the editor closes.</returns>
    [RelayCommand]
    private Task AddCardAsync() => _board.AddCardAsync(Column.Id);

    /// <summary>
    /// Opens the edit-column dialog.
    /// </summary>
    /// <returns>A task completing when the dialog closes.</returns>
    [RelayCommand]
    private Task EditAsync() => _board.EditColumnAsync(Column);

    /// <summary>
    /// Deletes the column with its cards.
    /// </summary>
    /// <returns>A task completing when the column is deleted or the deletion was declined.</returns>
    [RelayCommand]
    private Task DeleteAsync() => _board.DeleteColumnAsync(Column);
}
