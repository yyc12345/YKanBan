using CommunityToolkit.Mvvm.ComponentModel;

using YKanBan.Models;

namespace YKanBan.ViewModels.Dialogs;

/// <summary>
/// The move-card dialog: choose one of the other columns as the destination.
/// </summary>
public sealed partial class MoveCardViewModel : ViewModelBase {
    /// <summary>
    /// Initializes the dialog with the candidate destination columns.
    /// </summary>
    /// <param name="columns">All columns; the card's current column is excluded.</param>
    /// <param name="currentColumnId">The card's current column id.</param>
    public MoveCardViewModel(IReadOnlyList<ColumnModel> columns, long currentColumnId) {
        Targets = columns
            .Where(column => column.Id != currentColumnId)
            .Select(column => new OptionItem(column.Id, column.Title))
            .ToList();
        selectedTarget = Targets.FirstOrDefault();
    }

    /// <summary>
    /// Gets the candidate destination columns.
    /// </summary>
    public IReadOnlyList<OptionItem> Targets { get; }

    /// <summary>
    /// Gets or sets the selected destination column.
    /// </summary>
    [ObservableProperty]
    private OptionItem? selectedTarget;

    /// <summary>
    /// Gets a value indicating whether there is anywhere to move the card.
    /// </summary>
    public bool CanMove => Targets.Count > 0;
}
