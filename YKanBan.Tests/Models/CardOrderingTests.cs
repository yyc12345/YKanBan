using YKanBan.Models;
using YKanBan.Storage.AppData;

namespace YKanBan.Tests.Models;

/// <summary>
/// Tests for the in-column card display order: each sort option plus the id
/// ascending tie-breaker.
/// </summary>
[TestClass]
public class CardOrderingTests
{
    private static CardModel Card(long id, string title = "", long updatedAt = 0) =>
        new(id, 1, title, string.Empty, 0, updatedAt, []);

    private static long[] Ids(IEnumerable<CardModel> cards, CardSortOption option) =>
        CardOrdering.Sort(cards, option).Select(card => card.Id).ToArray();

    [TestMethod]
    public void IdSortsAscending()
    {
        CollectionAssert.AreEqual(new[] { 1L, 2L, 3L }, Ids([Card(3), Card(1), Card(2)], CardSortOption.Id));
    }

    [TestMethod]
    public void UpdatedAtSortsMostRecentFirstThenById()
    {
        CardModel[] cards = [Card(1, updatedAt: 10), Card(2, updatedAt: 30), Card(3, updatedAt: 10), Card(4, updatedAt: 20)];
        CollectionAssert.AreEqual(new[] { 2L, 4L, 1L, 3L }, Ids(cards, CardSortOption.UpdatedAt));
    }

    [TestMethod]
    public void TitleSortsByCodePointWithEmptyFirstThenById()
    {
        CardModel[] cards = [Card(1, "b"), Card(2, "B"), Card(3, ""), Card(4, "a"), Card(5, "b")];
        CollectionAssert.AreEqual(new[] { 3L, 2L, 4L, 1L, 5L }, Ids(cards, CardSortOption.Title));
    }

    [TestMethod]
    public void TitleSortsSupplementaryCharactersAfterBmpPrivateUse()
    {
        // U+1F600 is a surrogate pair in UTF-16, so ordinal comparison would put it before U+E000.
        CardModel[] cards = [Card(1, "\U0001F600"), Card(2, "")];
        CollectionAssert.AreEqual(new[] { 2L, 1L }, Ids(cards, CardSortOption.Title));
    }
}
