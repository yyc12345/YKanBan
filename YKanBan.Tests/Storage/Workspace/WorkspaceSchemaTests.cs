using Microsoft.Data.Sqlite;
using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Storage.Workspace;

/// <summary>
/// Verifies that the workspace DDL constraints reject invalid data at the door
/// and that the cascading delete chains work as specified.
/// </summary>
[TestClass]
public class WorkspaceSchemaTests
{
    /// <summary>
    /// Owns a temp directory plus a fresh workspace-schema database for a single test.
    /// </summary>
    private sealed class FreshDatabase : IDisposable
    {
        public TempDirectory Directory { get; } = new();

        public SqliteConnection Connection { get; }

        public FreshDatabase()
        {
            Connection = SqliteDatabase.Open(
                Path.Combine(Directory.FullPath, "ws.db"), WorkspaceSchema.Migrations);
        }

        /// <summary>
        /// Closes the connection before removing the temporary directory.
        /// </summary>
        public void Dispose()
        {
            Connection.Dispose();
            Directory.Dispose();
        }
    }

    [TestMethod]
    public void EmptyColumnTitleIsRejected()
    {
        using var db = new FreshDatabase();
        Assert.ThrowsExactly<SqliteException>(() =>
            SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('', 1, 1);"));
    }

    [TestMethod]
    public void WhitespaceColumnTitleIsAccepted()
    {
        using var db = new FreshDatabase();
        // No trimming: a single space is a non-empty title as far as the DDL is concerned.
        int affected = SqliteTestHelper.Exec(
            db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES (' ', 1, 1);");
        Assert.AreEqual(1, affected);
    }

    [TestMethod]
    public void DuplicateColumnTitleIsRejected()
    {
        using var db = new FreshDatabase();
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('Foo', 1, 1);");
        Assert.ThrowsExactly<SqliteException>(() =>
            SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('Foo', 1, 1);"));
    }

    [TestMethod]
    public void ColumnTitlesDifferingOnlyInAsciiCaseAreRejected()
    {
        using var db = new FreshDatabase();
        // NOCASE uniqueness: 'Foo' and 'foo' cannot coexist.
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('Foo', 1, 1);");
        Assert.ThrowsExactly<SqliteException>(() =>
            SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('foo', 1, 1);"));
    }

    [TestMethod]
    public void ColumnTitlesDifferingOnlyInNonAsciiCaseAreAccepted()
    {
        using var db = new FreshDatabase();
        // NOCASE folds ASCII only: 'Ä' and 'ä' remain distinct titles.
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('Ä', 1, 1);");
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('ä', 1, 1);");
        Assert.AreEqual(2L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM columns;"));
    }

    [TestMethod]
    public void ColumnTitleCaseOnlyRenameOfSameRowIsAccepted()
    {
        using var db = new FreshDatabase();
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('Foo', 1, 1);");
        int affected = SqliteTestHelper.Exec(db.Connection, "UPDATE columns SET title = 'foo' WHERE id = 1;");
        Assert.AreEqual(1, affected);
    }

    [TestMethod]
    public void ColumnTitlesStillOrderByCodePoint()
    {
        using var db = new FreshDatabase();
        // Uniqueness is NOCASE, but the column collation stays BINARY: 'B' (0x42) sorts before 'a' (0x61).
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('a', 1, 1);");
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('B', 1, 1);");
        Assert.AreEqual("B", SqliteTestHelper.ScalarString(db.Connection, "SELECT title FROM columns ORDER BY title LIMIT 1;"));
    }

    [TestMethod]
    public void EmptyTagNameIsRejected()
    {
        using var db = new FreshDatabase();
        Assert.ThrowsExactly<SqliteException>(() =>
            SqliteTestHelper.Exec(db.Connection, "INSERT INTO tags (name, color) VALUES ('', '#112233');"));
    }

    [TestMethod]
    public void TagNamesAcceptSpacesEmojiAndReservedWords()
    {
        using var db = new FreshDatabase();
        // Tag names are fully open: no charset or length rules, no trimming.
        foreach (string name in new[] { " ", "a b", "🏷️", "AND", "tag:with:colons" })
        {
            SqliteTestHelper.Exec(db.Connection, "INSERT INTO tags (name, color) VALUES ($n, '#112233');", ("$n", name));
        }
        Assert.AreEqual(5L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM tags;"));
    }

    [TestMethod]
    public void DuplicateTagNameIsRejected()
    {
        using var db = new FreshDatabase();
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO tags (name, color) VALUES ('bug', '#112233');");
        Assert.ThrowsExactly<SqliteException>(() =>
            SqliteTestHelper.Exec(db.Connection, "INSERT INTO tags (name, color) VALUES ('bug', '#445566');"));
    }

    [TestMethod]
    public void TagNamesDifferingOnlyInAsciiCaseAreRejected()
    {
        using var db = new FreshDatabase();
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO tags (name, color) VALUES ('bug', '#112233');");
        Assert.ThrowsExactly<SqliteException>(() =>
            SqliteTestHelper.Exec(db.Connection, "INSERT INTO tags (name, color) VALUES ('BUG', '#445566');"));
    }

    [TestMethod]
    public void TagNamesDifferingOnlyInNonAsciiCaseAreAccepted()
    {
        using var db = new FreshDatabase();
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO tags (name, color) VALUES ('Ä', '#112233');");
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO tags (name, color) VALUES ('ä', '#445566');");
        Assert.AreEqual(2L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM tags;"));
    }

    [TestMethod]
    [DataRow("#000000")]
    [DataRow("#FFFFFF")]
    [DataRow("#a1B2c3")]
    public void ValidTagColorsAreAccepted(string color)
    {
        using var db = new FreshDatabase();
        int affected = SqliteTestHelper.Exec(
            db.Connection, "INSERT INTO tags (name, color) VALUES ('x', $c);", ("$c", color));
        Assert.AreEqual(1, affected);
    }

    [TestMethod]
    [DataRow("112233")]
    [DataRow("#11223")]
    [DataRow("#1122334")]
    [DataRow("#G1B2C3")]
    [DataRow("#a1b2g3")]
    [DataRow(" #112233")]
    [DataRow("#112233 ")]
    [DataRow("")]
    public void InvalidTagColorsAreRejected(string color)
    {
        using var db = new FreshDatabase();
        Assert.ThrowsExactly<SqliteException>(() =>
            SqliteTestHelper.Exec(db.Connection, "INSERT INTO tags (name, color) VALUES ('x', $c);", ("$c", color)));
    }

    [TestMethod]
    public void CardRequiresExistingColumn()
    {
        using var db = new FreshDatabase();
        // foreign_keys=ON makes the FK enforceable; this is the DDL contract behind the cascades.
        Assert.ThrowsExactly<SqliteException>(() =>
            SqliteTestHelper.Exec(db.Connection, "INSERT INTO cards (column_id, created_at, updated_at) VALUES (999, 1, 1);"));
    }

    [TestMethod]
    public void CardDefaultsToEmptyTitleAndContent()
    {
        using var db = new FreshDatabase();
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('c', 1, 1);");
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO cards (column_id, created_at, updated_at) VALUES (1, 1, 1);");

        // No title is represented by the empty string.
        Assert.AreEqual(string.Empty, SqliteTestHelper.ScalarString(db.Connection, "SELECT title FROM cards WHERE id = 1;"));
        Assert.AreEqual(string.Empty, SqliteTestHelper.ScalarString(db.Connection, "SELECT content FROM cards WHERE id = 1;"));
    }

    [TestMethod]
    public void CardTagDuplicateAssignmentIsRejected()
    {
        using var db = new FreshDatabase();
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('c', 1, 1);");
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO cards (column_id, created_at, updated_at) VALUES (1, 1, 1);");
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO tags (name, color) VALUES ('bug', '#112233');");
        SqliteTestHelper.Exec(db.Connection, "INSERT INTO card_tags (card_id, tag_id) VALUES (1, 1);");

        // Composite primary key: the same (card, tag) pair cannot be inserted twice.
        Assert.ThrowsExactly<SqliteException>(() =>
            SqliteTestHelper.Exec(db.Connection, "INSERT INTO card_tags (card_id, tag_id) VALUES (1, 1);"));
    }

    [TestMethod]
    public void DeletingColumnCascadesToCardsAndCardTags()
    {
        using var db = new FreshDatabase();
        InsertBoardRow(db.Connection);

        SqliteTestHelper.Exec(db.Connection, "DELETE FROM columns WHERE id = 1;");

        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM columns;"));
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM cards;"));
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM card_tags;"));
        // Tags are never cleaned up automatically.
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM tags;"));
    }

    [TestMethod]
    public void DeletingCardCascadesToCardTagsOnly()
    {
        using var db = new FreshDatabase();
        InsertBoardRow(db.Connection);

        SqliteTestHelper.Exec(db.Connection, "DELETE FROM cards WHERE id = 1;");

        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM columns;"));
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM cards;"));
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM card_tags;"));
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM tags;"));
    }

    [TestMethod]
    public void DeletingTagRemovesAssignmentsButKeepsCards()
    {
        using var db = new FreshDatabase();
        InsertBoardRow(db.Connection);

        SqliteTestHelper.Exec(db.Connection, "DELETE FROM tags WHERE id = 1;");

        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM tags;"));
        Assert.AreEqual(0L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM card_tags;"));
        // The card merely loses the tag.
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM cards;"));
        Assert.AreEqual(1L, SqliteTestHelper.ScalarLong(db.Connection, "SELECT COUNT(*) FROM columns;"));
    }

    /// <summary>
    /// Inserts one column, one card, one tag and their assignment, ready for a cascade test.
    /// </summary>
    /// <param name="connection">An open workspace-schema connection.</param>
    private static void InsertBoardRow(SqliteConnection connection)
    {
        SqliteTestHelper.Exec(connection, "INSERT INTO columns (title, created_at, updated_at) VALUES ('c', 1, 1);");
        SqliteTestHelper.Exec(connection, "INSERT INTO cards (column_id, created_at, updated_at) VALUES (1, 1, 1);");
        SqliteTestHelper.Exec(connection, "INSERT INTO tags (name, color) VALUES ('bug', '#112233');");
        SqliteTestHelper.Exec(connection, "INSERT INTO card_tags (card_id, tag_id) VALUES (1, 1);");
    }
}
