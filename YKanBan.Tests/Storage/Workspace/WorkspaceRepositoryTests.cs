using Microsoft.Data.Sqlite;
using YKanBan.Models;
using YKanBan.Storage.AppData;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>
/// Tests for workspace CRUD, the three cascade chains, edit-confirm
/// transaction atomicity, timestamp refresh rules and the last_edited_at
/// write-through.
/// </summary>
[TestClass]
public class WorkspaceRepositoryTests
{
    /// <summary>
    /// Owns a workspace with fixed-timestamp fixture data (one column, one
    /// card, two tags linked to the card), a fake-clock app.db and the
    /// repository under test.
    /// </summary>
    private sealed class RepositoryFixture : IDisposable
    {
        public TempDirectory Directory { get; } = new();

        public long Clock { get; set; } = 1000;

        public AppDatabase AppDb { get; }

        public WorkspaceRepository Repository { get; }

        public long ColumnId { get; } = 1;

        public long CardId { get; } = 1;

        public long BugTagId { get; } = 1;

        public long UiTagId { get; } = 2;

        public RepositoryFixture()
        {
            WorkspaceInitializer.Initialize(Directory.FullPath);

            // Fixture rows carry literal timestamps so refresh vs. no-refresh stays observable.
            using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(Directory.FullPath);
            SqliteTestHelper.Exec(connection, "INSERT INTO columns (title, description, created_at, updated_at) VALUES ('Col', '', 100, 100);");
            SqliteTestHelper.Exec(connection, "INSERT INTO cards (column_id, title, content, created_at, updated_at) VALUES (1, 'T', 'C', 100, 100);");
            SqliteTestHelper.Exec(connection, "INSERT INTO tags (name, color, description) VALUES ('bug', '#ff0000', '');");
            SqliteTestHelper.Exec(connection, "INSERT INTO tags (name, color, description) VALUES ('ui', '#00ff00', '');");
            SqliteTestHelper.Exec(connection, "INSERT INTO card_tags (card_id, tag_id) VALUES (1, 1);");
            SqliteTestHelper.Exec(connection, "INSERT INTO card_tags (card_id, tag_id) VALUES (1, 2);");

            AppDb = new AppDatabase(Path.Combine(Directory.FullPath, "app.db"), () => Clock);
            Repository = new WorkspaceRepository(Directory.FullPath, AppDb);
        }

        /// <summary>The last_edited_at currently mirrored into the registry.</summary>
        public long LastEditedAt => AppDb.GetWorkspaces().Single().LastEditedAt;

        public void Dispose()
        {
            Repository.Dispose();
            AppDb.Dispose();
            Directory.Dispose();
        }
    }

    [TestMethod]
    public void UpdateColumnWithoutChangeWritesNothing()
    {
        using var fixture = new RepositoryFixture();
        ColumnModel before = fixture.Repository.GetColumn(fixture.ColumnId)!;

        ColumnModel result = fixture.Repository.UpdateColumn(fixture.ColumnId, "Col", "");

        Assert.AreEqual(before.UpdatedAt, result.UpdatedAt);
        Assert.AreEqual(0, fixture.AppDb.GetWorkspaces().Count);
    }

    [TestMethod]
    public void UpdateColumnChangeRefreshesColumnTimestampAndLastEdited()
    {
        using var fixture = new RepositoryFixture();
        fixture.Clock = 2000;

        ColumnModel updated = fixture.Repository.UpdateColumn(fixture.ColumnId, "Col2", "desc");

        Assert.IsTrue(updated.UpdatedAt > 100L);
        Assert.AreEqual(2000L, fixture.LastEditedAt);
    }

    [TestMethod]
    public void DeleteColumnCascadesToCardsAndTagLinks()
    {
        using var fixture = new RepositoryFixture();
        fixture.Clock = 2000;

        Assert.IsTrue(fixture.Repository.DeleteColumn(fixture.ColumnId));

        Assert.AreEqual(0, fixture.Repository.GetCards().Count);
        // The tags themselves survive the column deletion.
        Assert.AreEqual(2, fixture.Repository.GetTags().Count);
        using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(fixture.Directory.FullPath);
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM card_tags;"));
        Assert.AreEqual(2000L, fixture.LastEditedAt);
    }

    [TestMethod]
    public void DeleteCardRemovesTagLinksButKeepsTags()
    {
        using var fixture = new RepositoryFixture();

        Assert.IsTrue(fixture.Repository.DeleteCard(fixture.CardId));

        Assert.IsNull(fixture.Repository.GetCard(fixture.CardId));
        Assert.AreEqual(2, fixture.Repository.GetTags().Count);
        using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(fixture.Directory.FullPath);
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM card_tags;"));
    }

    [TestMethod]
    public void DeleteTagDetachesFromCardsButKeepsCards()
    {
        using var fixture = new RepositoryFixture();

        Assert.IsTrue(fixture.Repository.DeleteTag(fixture.BugTagId));

        Assert.AreEqual(1, fixture.Repository.GetTags().Count);
        CardModel card = fixture.Repository.GetCard(fixture.CardId)!;
        Assert.AreEqual(1, card.Tags.Count);
        Assert.AreEqual("ui", card.Tags[0].Name);
    }

    [TestMethod]
    public void UpdateCardWithoutEffectiveChangeWritesNothing()
    {
        using var fixture = new RepositoryFixture();
        fixture.Clock = 2000;
        CardModel before = fixture.Repository.GetCard(fixture.CardId)!;

        CardModel result = fixture.Repository.UpdateCard(fixture.CardId, "T", "C", [fixture.BugTagId, fixture.UiTagId]);

        Assert.AreEqual(before.UpdatedAt, result.UpdatedAt);
        Assert.AreEqual(0, fixture.AppDb.GetWorkspaces().Count);
    }

    [TestMethod]
    public void UpdateCardTitleChangeRefreshesTimestampsAndLastEdited()
    {
        using var fixture = new RepositoryFixture();
        fixture.Clock = 2000;

        CardModel updated = fixture.Repository.UpdateCard(fixture.CardId, "T2", "C", [fixture.BugTagId, fixture.UiTagId]);

        Assert.IsTrue(updated.UpdatedAt > 100L);
        Assert.AreEqual(2000L, fixture.LastEditedAt);
    }

    [TestMethod]
    public void UpdateCardTagSetOnlyChangeStillRefreshesCardTimestamp()
    {
        using var fixture = new RepositoryFixture();
        fixture.Clock = 2000;

        CardModel updated = fixture.Repository.UpdateCard(fixture.CardId, "T", "C", [fixture.BugTagId]);

        Assert.IsTrue(updated.UpdatedAt > 100L);
        Assert.AreEqual(1, updated.Tags.Count);
        Assert.AreEqual(2000L, fixture.LastEditedAt);
    }

    [TestMethod]
    public void UpdateCardWithUnknownTagRollsBackAtomically()
    {
        using var fixture = new RepositoryFixture();
        fixture.Clock = 2000;

        // Tag 999 does not exist: the FK fires mid-transaction and the whole edit must roll back.
        Assert.ThrowsExactly<SqliteException>(() =>
            fixture.Repository.UpdateCard(fixture.CardId, "TX", "CX", [fixture.BugTagId, 999]));

        CardModel after = fixture.Repository.GetCard(fixture.CardId)!;
        Assert.AreEqual("T", after.Title);
        Assert.AreEqual("C", after.Content);
        Assert.AreEqual(100L, after.UpdatedAt);
        Assert.AreEqual(2, after.Tags.Count);
        Assert.AreEqual(0, fixture.AppDb.GetWorkspaces().Count);
    }

    [TestMethod]
    public void MoveCardRefreshesLastEditedButNotCardTimestamp()
    {
        using var fixture = new RepositoryFixture();
        long target = fixture.Repository.AddColumn("Target", "").Id;
        fixture.Clock = 2000;

        Assert.IsTrue(fixture.Repository.MoveCard(fixture.CardId, target));

        CardModel moved = fixture.Repository.GetCard(fixture.CardId)!;
        Assert.AreEqual(target, moved.ColumnId);
        // The move is a write but deliberately leaves the card's updated_at alone.
        Assert.AreEqual(100L, moved.UpdatedAt);
        Assert.AreEqual(2000L, fixture.LastEditedAt);
    }

    [TestMethod]
    public void MoveCardToCurrentColumnWritesNothing()
    {
        using var fixture = new RepositoryFixture();
        fixture.Clock = 2000;

        Assert.IsFalse(fixture.Repository.MoveCard(fixture.CardId, fixture.ColumnId));

        Assert.AreEqual(0, fixture.AppDb.GetWorkspaces().Count);
    }

    [TestMethod]
    public void AddCardCreatesWithEqualTimestampsAndLinkedTags()
    {
        using var fixture = new RepositoryFixture();
        fixture.Clock = 2000;

        CardModel card = fixture.Repository.AddCard(fixture.ColumnId, "N", "body", [fixture.UiTagId]);

        Assert.IsTrue(card.CreatedAt > 0L);
        Assert.AreEqual(card.CreatedAt, card.UpdatedAt);
        Assert.AreEqual(1, card.Tags.Count);
        Assert.AreEqual("ui", card.Tags[0].Name);
        Assert.AreEqual(2000L, fixture.LastEditedAt);
    }

    [TestMethod]
    public void UpdateTagWithoutChangeWritesNothing()
    {
        using var fixture = new RepositoryFixture();
        fixture.Clock = 2000;

        TagModel result = fixture.Repository.UpdateTag(fixture.BugTagId, "bug", RgbColor.Parse("#ff0000"), "");

        Assert.AreEqual("bug", result.Name);
        Assert.AreEqual(0, fixture.AppDb.GetWorkspaces().Count);
    }

    [TestMethod]
    public void UpdateTagChangeWritesAndTouchesLastEdited()
    {
        using var fixture = new RepositoryFixture();
        fixture.Clock = 3000;

        TagModel updated = fixture.Repository.UpdateTag(fixture.BugTagId, "defect", RgbColor.Parse("#ee0a0a"), "desc");

        Assert.AreEqual("defect", updated.Name);
        Assert.AreEqual(new RgbColor(0xEE, 0x0A, 0x0A), updated.Color);
        Assert.AreEqual("desc", updated.Description);
        Assert.AreEqual(3000L, fixture.LastEditedAt);
    }

    [TestMethod]
    public void GetTagsWithUsageCountsAssignments()
    {
        using var fixture = new RepositoryFixture();

        IReadOnlyList<(TagModel Tag, long UsageCount)> before = fixture.Repository.GetTagsWithUsage();
        Assert.AreEqual(1L, before.Single(entry => entry.Tag.Name == "bug").UsageCount);
        Assert.AreEqual(1L, before.Single(entry => entry.Tag.Name == "ui").UsageCount);

        // Detaching one tag via an edit confirmation drops its usage to zero but keeps the tag.
        fixture.Clock = 2000;
        fixture.Repository.UpdateCard(fixture.CardId, "T", "C", [fixture.UiTagId]);

        IReadOnlyList<(TagModel Tag, long UsageCount)> after = fixture.Repository.GetTagsWithUsage();
        Assert.AreEqual(0L, after.Single(entry => entry.Tag.Name == "bug").UsageCount);
        Assert.AreEqual(1L, after.Single(entry => entry.Tag.Name == "ui").UsageCount);
    }
}
