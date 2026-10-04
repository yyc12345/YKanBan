using Avalonia.Controls;
using Avalonia.Input;

using YKanBan.ViewModels.Board;

namespace YKanBan.Views.Board;

public partial class CardView : UserControl {
    public CardView() {
        InitializeComponent();
    }

    /// <summary>
    /// Clicking the card body opens the card editor.
    /// </summary>
    private void OnCardTapped(object? sender, TappedEventArgs e) {
        if (DataContext is CardViewModel card) {
            card.EditCommand.Execute(null);
        }
    }

    /// <summary>
    /// Stops the corner menu button's tap from bubbling to the card body.
    /// </summary>
    private void OnMenuButtonTapped(object? sender, TappedEventArgs e) => e.Handled = true;
}
