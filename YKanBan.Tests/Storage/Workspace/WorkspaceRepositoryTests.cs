using Microsoft.Data.Sqlite;

using YKanBan.Models;
using YKanBan.Search;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>
/// Tests for workspace CRUD, the three cascade chains, edit-confirm
/// transaction atomicity and the timestamp refresh rules.
/// </summary>
[TestClass]
public class WorkspaceRepositoryTests {
    /// <summary>
    /// Owns a workspace with fixed-timestamp fixture data (one column, one
    /// card, two tags linked to the card) plus the repository under test.
    /// Fixture timestamps are small literal values so that any real write,
    /// which stamps the current Unix time, is observable.
    /// </summary>
    private sealed class RepositoryFixture : IDisposable {
        public TempDirectory Directory { get; } = new();

        public WorkspaceRepository Repository { get; }

        public long ColumnId { get; } = 1;

        public long CardId { get; } = 1;

        public long BugTagId { get; } = 1;

        public long UiTagId { get; } = 2;

        public RepositoryFixture() {
            WorkspaceInitializer.Initialize(Directory.FullPath);

            using (SqliteConnection connection = SqliteTestHelper.OpenWorkspace(Directory.FullPath)) {
                SqliteTestHelper.Exec(connection, "INSERT INTO columns (title, description, created_at, updated_at) VALUES ('Col', '', 100, 100);");
                SqliteTestHelper.Exec(connection, "INSERT INTO cards (column_id, title, content, created_at, updated_at) VALUES (1, 'T', 'C', 100, 100);");
                SqliteTestHelper.Exec(connection, "INSERT INTO tags (name, color, description) VALUES ('bug', '#ff0000', '');");
                SqliteTestHelper.Exec(connection, "INSERT INTO tags (name, color, description) VALUES ('ui', '#00ff00', '');");
                SqliteTestHelper.Exec(connection, "INSERT INTO card_tags (card_id, tag_id) VALUES (1, 1);");
                SqliteTestHelper.Exec(connection, "INSERT INTO card_tags (card_id, tag_id) VALUES (1, 2);");
            }

            Repository = new WorkspaceRepository(Directory.FullPath);
        }

        public void Dispose() {
            Repository.Dispose();
            Directory.Dispose();
        }
    }

    [TestMethod]
    public void GetColumnsReturnsTheSeededColumn() {
        using var fixture = new RepositoryFixture();
        ColumnModel column = fixture.Repository.GetColumn(fixture.ColumnId)!;
        Assert.AreEqual("Col", column.Title);
        Assert.AreEqual(1, fixture.Repository.GetColumns().Count);
    }

    [TestMethod]
    public void AddColumnSetsEqualTimestamps() {
        using var fixture = new RepositoryFixture();
        ColumnModel column = fixture.Repository.AddColumn("New", "desc");
        Assert.IsTrue(column.CreatedAt > 0L);
        Assert.AreEqual(column.CreatedAt, column.UpdatedAt);
    }

    [TestMethod]
    public void UpdateColumnWithoutChangeWritesNothing() {
        using var fixture = new RepositoryFixture();

        ColumnModel result = fixture.Repository.UpdateColumn(fixture.ColumnId, "Col", "");

        // A no-op must not refresh the fixed fixture timestamp.
        Assert.AreEqual(100L, result.UpdatedAt);
    }

    [TestMethod]
    public void UpdateColumnChangeRefreshesTimestamp() {
        using var fixture = new RepositoryFixture();

        ColumnModel updated = fixture.Repository.UpdateColumn(fixture.ColumnId, "Col2", "desc");

        Assert.IsTrue(updated.UpdatedAt > 100L);
        Assert.AreEqual(100L, updated.CreatedAt);
    }

    [TestMethod]
    public void DeleteColumnCascadesToCardsAndTagLinks() {
        using var fixture = new RepositoryFixture();

        Assert.IsTrue(fixture.Repository.DeleteColumn(fixture.ColumnId));

        Assert.AreEqual(0, fixture.Repository.GetCards().Count);
        // The tags themselves survive the column deletion.
        Assert.AreEqual(2, fixture.Repository.GetTags().Count);
        using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(fixture.Directory.FullPath);
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM card_tags;"));
    }

    [TestMethod]
    public void DeleteCardRemovesTagLinksButKeepsTags() {
        using var fixture = new RepositoryFixture();

        Assert.IsTrue(fixture.Repository.DeleteCard(fixture.CardId));

        Assert.IsNull(fixture.Repository.GetCard(fixture.CardId));
        Assert.AreEqual(2, fixture.Repository.GetTags().Count);
        using SqliteConnection connection = SqliteTestHelper.OpenWorkspace(fixture.Directory.FullPath);
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(connection, "SELECT COUNT(*) FROM card_tags;"));
    }

    [TestMethod]
    public void DeleteTagDetachesFromCardsButKeepsCards() {
        using var fixture = new RepositoryFixture();

        Assert.IsTrue(fixture.Repository.DeleteTag(fixture.BugTagId));

        Assert.AreEqual(1, fixture.Repository.GetTags().Count);
        CardModel card = fixture.Repository.GetCard(fixture.CardId)!;
        Assert.AreEqual(1, card.Tags.Count);
        Assert.AreEqual("ui", card.Tags[0].Name);
    }

    [TestMethod]
    public void UpdateCardWithoutEffectiveChangeWritesNothing() {
        using var fixture = new RepositoryFixture();

        CardModel result = fixture.Repository.UpdateCard(
            fixture.CardId, "T", "C", [fixture.BugTagId, fixture.UiTagId]);

        Assert.AreEqual(100L, result.UpdatedAt);
    }

    [TestMethod]
    public void UpdateCardTitleChangeRefreshesTimestamp() {
        using var fixture = new RepositoryFixture();

        CardModel updated = fixture.Repository.UpdateCard(
            fixture.CardId, "T2", "C", [fixture.BugTagId, fixture.UiTagId]);

        Assert.IsTrue(updated.UpdatedAt > 100L);
        Assert.AreEqual(100L, updated.CreatedAt);
    }

    [TestMethod]
    public void UpdateCardTagSetOnlyChangeStillRefreshesTimestamp() {
        using var fixture = new RepositoryFixture();

        CardModel updated = fixture.Repository.UpdateCard(fixture.CardId, "T", "C", [fixture.BugTagId]);

        Assert.IsTrue(updated.UpdatedAt > 100L);
        Assert.AreEqual(1, updated.Tags.Count);
    }

    [TestMethod]
    public void UpdateCardWithUnknownTagRollsBackAtomically() {
        using var fixture = new RepositoryFixture();

        // Tag 999 does not exist: the FK fires mid-transaction and the whole edit must roll back.
        Assert.ThrowsExactly<SqliteException>(() =>
            fixture.Repository.UpdateCard(fixture.CardId, "TX", "CX", [fixture.BugTagId, 999]));

        CardModel after = fixture.Repository.GetCard(fixture.CardId)!;
        Assert.AreEqual("T", after.Title);
        Assert.AreEqual("C", after.Content);
        Assert.AreEqual(100L, after.UpdatedAt);
        Assert.AreEqual(2, after.Tags.Count);
    }

    [TestMethod]
    public void MoveCardUpdatesColumnWithoutRefreshingTimestamp() {
        using var fixture = new RepositoryFixture();
        long target = fixture.Repository.AddColumn("Target", "").Id;

        Assert.IsTrue(fixture.Repository.MoveCard(fixture.CardId, target));

        CardModel moved = fixture.Repository.GetCard(fixture.CardId)!;
        Assert.AreEqual(target, moved.ColumnId);
        // The move is a write but deliberately leaves the card's updated_at alone.
        Assert.AreEqual(100L, moved.UpdatedAt);
    }

    [TestMethod]
    public void MoveCardToCurrentColumnIsNoOp() {
        using var fixture = new RepositoryFixture();

        Assert.IsFalse(fixture.Repository.MoveCard(fixture.CardId, fixture.ColumnId));

        Assert.AreEqual(100L, fixture.Repository.GetCard(fixture.CardId)!.UpdatedAt);
    }

    [TestMethod]
    public void AddCardCreatesWithEqualTimestampsAndLinkedTags() {
        using var fixture = new RepositoryFixture();

        CardModel card = fixture.Repository.AddCard(fixture.ColumnId, "N", "body", [fixture.UiTagId]);

        Assert.IsTrue(card.CreatedAt > 0L);
        Assert.AreEqual(card.CreatedAt, card.UpdatedAt);
        Assert.AreEqual(1, card.Tags.Count);
        Assert.AreEqual("ui", card.Tags[0].Name);
    }

    [TestMethod]
    public void UpdateTagWithoutChangeWritesNothing() {
        using var fixture = new RepositoryFixture();

        TagModel result = fixture.Repository.UpdateTag(
            fixture.BugTagId, "bug", RgbColor.Parse("#ff0000"), "");

        Assert.AreEqual("bug", result.Name);
        Assert.AreEqual(new RgbColor(0xFF, 0x00, 0x00), result.Color);
    }

    [TestMethod]
    public void UpdateTagChangeWrites() {
        using var fixture = new RepositoryFixture();

        TagModel updated = fixture.Repository.UpdateTag(
            fixture.BugTagId, "defect", RgbColor.Parse("#ee0a0a"), "desc");

        Assert.AreEqual("defect", updated.Name);
        Assert.AreEqual(new RgbColor(0xEE, 0x0A, 0x0A), updated.Color);
        Assert.AreEqual("desc", updated.Description);
        Assert.AreEqual("defect", fixture.Repository.GetTags().Single(tag => tag.Id == fixture.BugTagId).Name);
    }

    [TestMethod]
    public void AddTagWithDuplicateNameIsRejected() {
        using var fixture = new RepositoryFixture();
        Assert.ThrowsExactly<SqliteException>(() =>
            fixture.Repository.AddTag("bug", RgbColor.Parse("#123456"), ""));
    }

    [TestMethod]
    public void GetCardIdsMatchingRunsTheCompiledSearch() {
        using var fixture = new RepositoryFixture();
        CompiledSearch compiled = SearchSqlCompiler.Compile(new SearchNode.FreeText("T"));

        IReadOnlyCollection<long> ids = fixture.Repository.GetCardIdsMatching(compiled);

        CollectionAssert.AreEquivalent(new[] { fixture.CardId }, ids.ToList());
    }

    [TestMethod]
    public void GetTagsWithUsageCountsAssignments() {
        using var fixture = new RepositoryFixture();

        IReadOnlyList<(TagModel Tag, long UsageCount)> before = fixture.Repository.GetTagsWithUsage();
        Assert.AreEqual(1L, before.Single(entry => entry.Tag.Name == "bug").UsageCount);
        Assert.AreEqual(1L, before.Single(entry => entry.Tag.Name == "ui").UsageCount);

        // Detaching one tag via an edit confirmation drops its usage to zero but keeps the tag.
        fixture.Repository.UpdateCard(fixture.CardId, "T", "C", [fixture.UiTagId]);

        IReadOnlyList<(TagModel Tag, long UsageCount)> after = fixture.Repository.GetTagsWithUsage();
        Assert.AreEqual(0L, after.Single(entry => entry.Tag.Name == "bug").UsageCount);
        Assert.AreEqual(1L, after.Single(entry => entry.Tag.Name == "ui").UsageCount);
    }
}
