using Avalonia.Controls;

using YKanBan.ViewModels.Board;
using YKanBan.ViewModels.Dialogs;
using YKanBan.Views.Dialogs;

namespace YKanBan.Views.Board;

public partial class BoardView : UserControl {
    private bool _registered;

    public BoardView() {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);
        if (DataContext is BoardViewModel board && !_registered) {
            _registered = true;
            board.AddColumn.RegisterHandler(_ => EditColumnAsync(board, null));
            board.EditColumn.RegisterHandler(column => EditColumnAsync(board, column));
            board.DeleteColumn.RegisterHandler(column => DeleteColumnAsync(board, column));
            board.AddCard.RegisterHandler(column => EditCardAsync(board, column, null));
            board.EditCard.RegisterHandler(card => EditCardAsync(board, null, card));
            board.DeleteCard.RegisterHandler(card => DeleteCardAsync(board, card));
            board.MoveCard.RegisterHandler(card => MoveCardAsync(board, card));
            board.ShowSearchError.RegisterHandler(ShowSearchErrorAsync);
        }
    }

    /// <summary>
    /// Gets the window that owns this board, used as the dialog owner.
    /// </summary>
    private Window? OwningWindow => TopLevel.GetTopLevel(this) as Window;

    /// <summary>
    /// Shows the add/edit column dialog and reloads on success.
    /// </summary>
    private async Task EditColumnAsync(BoardViewModel board, ColumnViewModel? column) {
        if (OwningWindow is not Window owner) {
            return;
        }

        var dialog = new ColumnEditorDialog {
            DataContext = new ColumnEditorViewModel(board.Repository, column?.Model),
        };
        if (await dialog.ShowDialog<bool>(owner)) {
            board.Reload();
        }
    }

    /// <summary>
    /// Confirms and deletes a column, then reloads.
    /// </summary>
    private async Task DeleteColumnAsync(BoardViewModel board, ColumnViewModel column) {
        if (OwningWindow is not Window owner) {
            return;
        }

        bool confirmed = true;
        if (board.Config.Confirmations.DeleteColumn) {
            var dialog = new DeleteColumnConfirmDialog {
                DataContext = new DeleteColumnConfirmViewModel(column.Title, column.CardCount),
            };
            confirmed = await dialog.ShowDialog<bool>(owner);
        }

        if (!confirmed) {
            return;
        }

        board.Repository.DeleteColumn(column.Id);
        board.Reload();
    }

    /// <summary>
    /// Shows the add/edit card dialog and reloads on success.
    /// </summary>
    private async Task EditCardAsync(BoardViewModel board, ColumnViewModel? column, CardViewModel? card) {
        if (OwningWindow is not Window owner) {
            return;
        }

        long columnId = card?.ColumnId ?? column!.Id;
        var dialog = new CardEditorDialog {
            DataContext = new CardEditorViewModel(board.Repository, columnId, card?.Model),
        };
        if (await dialog.ShowDialog<bool>(owner)) {
            board.Reload();
        }
    }

    /// <summary>
    /// Confirms and deletes a card, then reloads.
    /// </summary>
    private async Task DeleteCardAsync(BoardViewModel board, CardViewModel card) {
        if (OwningWindow is not Window owner) {
            return;
        }

        bool confirmed = true;
        if (board.Config.Confirmations.DeleteCard) {
            var dialog = new DeleteCardConfirmDialog {
                DataContext = new DeleteCardConfirmViewModel(card.Id),
            };
            confirmed = await dialog.ShowDialog<bool>(owner);
        }

        if (!confirmed) {
            return;
        }

        board.Repository.DeleteCard(card.Id);
        board.Reload();
    }

    /// <summary>
    /// Shows the move-card dialog and reloads when a destination is chosen.
    /// </summary>
    private async Task MoveCardAsync(BoardViewModel board, CardViewModel card) {
        if (OwningWindow is not Window owner) {
            return;
        }

        var dialog = new MoveCardDialog {
            DataContext = new MoveCardViewModel(board.Repository.GetColumns(), card.ColumnId),
        };
        if (await dialog.ShowDialog<long?>(owner) is long targetColumnId) {
            board.Repository.MoveCard(card.Id, targetColumnId);
            board.Reload();
        }
    }

    /// <summary>
    /// Shows the search syntax error dialog.
    /// </summary>
    private async Task ShowSearchErrorAsync(string detail) {
        if (OwningWindow is not Window owner) {
            return;
        }

        var dialog = new SearchErrorDialog {
            DataContext = new SearchErrorViewModel(detail),
        };
        await dialog.ShowDialog(owner);
    }
}
