using Microsoft.Data.Sqlite;
using YKanBan.Search;
using YKanBan.Storage;
using YKanBan.Storage.Workspace;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Search;

/// <summary>
/// Tests for the query-to-SQL compiler, executed end-to-end against a
/// fixture workspace database.
/// </summary>
[TestClass]
public class SearchSqlCompilerTests
{
    /// <summary>Owns a workspace with a fixed card/tag fixture.</summary>
    private sealed class SearchFixture : IDisposable
    {
        public TempDirectory Directory { get; } = new();

        public SqliteConnection Connection { get; }

        public SearchFixture()
        {
            WorkspaceInitializer.Initialize(Directory.FullPath);
            using (SqliteConnection setup = SqliteTestHelper.OpenWorkspace(Directory.FullPath))
            {
                // Columns: 1 "To Do", 2 "In Progress".
                SqliteTestHelper.Exec(setup, "INSERT INTO columns (title, description, created_at, updated_at) VALUES ('To Do', '', 1, 1);");
                SqliteTestHelper.Exec(setup, "INSERT INTO columns (title, description, created_at, updated_at) VALUES ('In Progress', '', 1, 1);");

                // Cards: 1 titled, 2 title-or-content matches, 3 untitled, 4 wildcard chars.
                SqliteTestHelper.Exec(setup, "INSERT INTO cards (column_id, title, content, created_at, updated_at) VALUES (1, 'Fix login', 'Cannot sign in', 1, 1);");
                SqliteTestHelper.Exec(setup, "INSERT INTO cards (column_id, title, content, created_at, updated_at) VALUES (2, 'Green apple', 'the apple pie', 1, 1);");
                SqliteTestHelper.Exec(setup, "INSERT INTO cards (column_id, title, content, created_at, updated_at) VALUES (2, '', 'banana split', 1, 1);");
                SqliteTestHelper.Exec(setup, "INSERT INTO cards (column_id, title, content, created_at, updated_at) VALUES (1, '100% done', 'a_b test', 1, 1);");

                // Tags: mixed case names plus one containing spaces.
                SqliteTestHelper.Exec(setup, "INSERT INTO tags (name, color, description) VALUES ('Bug', '#ff0000', '');");
                SqliteTestHelper.Exec(setup, "INSERT INTO tags (name, color, description) VALUES ('UI', '#00ff00', '');");
                SqliteTestHelper.Exec(setup, "INSERT INTO tags (name, color, description) VALUES ('release notes', '#0000ff', '');");
                SqliteTestHelper.Exec(setup, "INSERT INTO card_tags (card_id, tag_id) VALUES (1, 1);");
                SqliteTestHelper.Exec(setup, "INSERT INTO card_tags (card_id, tag_id) VALUES (2, 2);");
                SqliteTestHelper.Exec(setup, "INSERT INTO card_tags (card_id, tag_id) VALUES (3, 2);");
                SqliteTestHelper.Exec(setup, "INSERT INTO card_tags (card_id, tag_id) VALUES (4, 3);");
            }

            Connection = SqliteTestHelper.OpenWorkspace(Directory.FullPath);
        }

        /// <summary>Runs a query and returns the ids of the matching cards.</summary>
        public List<long> Search(string queryText)
        {
            SearchNode? query = SearchQueryParser.Parse(queryText);
            Assert.IsNotNull(query, "Blank fixture queries are not expected here.");
            CompiledSearch compiled = SearchSqlCompiler.Compile(query);

            using var command = Connection.CreateCommand();
            command.CommandText = $"SELECT c.id FROM cards c WHERE {compiled.Predicate} ORDER BY c.id ASC;";
            foreach ((string name, object value) in compiled.Parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }
            using var reader = command.ExecuteReader();

            var ids = new List<long>();
            while (reader.Read())
            {
                ids.Add(reader.GetInt64(0));
            }
            return ids;
        }

        public void Dispose()
        {
            Connection.Dispose();
            Directory.Dispose();
        }
    }

    [TestMethod]
    public void FreeTextMatchesTitleOrContentCaseInsensitively()
    {
        using var fixture = new SearchFixture();
        CollectionAssert.AreEqual(new[] { 2L }, fixture.Search("apple"));
        CollectionAssert.AreEqual(new[] { 1L }, fixture.Search("login"));
        CollectionAssert.AreEqual(new[] { 2L }, fixture.Search("APPLE"));
        CollectionAssert.AreEqual(new[] { 2L }, fixture.Search("pie"));
    }

    [TestMethod]
    public void FieldQualifiersScopeToTheirField()
    {
        using var fixture = new SearchFixture();
        CollectionAssert.AreEqual(new[] { 2L }, fixture.Search("title:apple"));
        CollectionAssert.AreEqual(new[] { 3L }, fixture.Search("content:banana"));
        Assert.AreEqual(0, fixture.Search("title:pie").Count);
    }

    [TestMethod]
    public void TagQualifierMatchesExactlyAndCaseInsensitively()
    {
        using var fixture = new SearchFixture();
        CollectionAssert.AreEqual(new[] { 1L }, fixture.Search("tag:bug"));
        CollectionAssert.AreEqual(new[] { 2L, 3L }, fixture.Search("tag:ui"));
        CollectionAssert.AreEqual(new[] { 4L }, fixture.Search("tag:\"release notes\""));

        // Exact match: prefixes and substrings do not hit.
        Assert.AreEqual(0, fixture.Search("tag:bu").Count);
        Assert.AreEqual(0, fixture.Search("tag:release").Count);
    }

    [TestMethod]
    public void ColumnQualifierMatchesColumnTitleExactly()
    {
        using var fixture = new SearchFixture();
        CollectionAssert.AreEqual(new[] { 1L, 4L }, fixture.Search("column:\"to do\""));
        CollectionAssert.AreEqual(new[] { 2L, 3L }, fixture.Search("column:\"in progress\""));

        // Not a prefix or substring match.
        Assert.AreEqual(0, fixture.Search("column:To").Count);
        Assert.AreEqual(0, fixture.Search("column:progress").Count);
    }

    [TestMethod]
    public void IdQualifierMatchesExactly()
    {
        using var fixture = new SearchFixture();
        CollectionAssert.AreEqual(new[] { 3L }, fixture.Search("id:3"));
        Assert.AreEqual(0, fixture.Search("id:99").Count);
    }

    [TestMethod]
    public void AndOrCombination()
    {
        using var fixture = new SearchFixture();
        CollectionAssert.AreEqual(new[] { 2L, 3L }, fixture.Search("apple OR banana"));
        CollectionAssert.AreEqual(new[] { 2L }, fixture.Search("apple AND id:2"));
        Assert.AreEqual(0, fixture.Search("apple AND id:1").Count);
    }

    [TestMethod]
    public void ParenthesizedGrouping()
    {
        using var fixture = new SearchFixture();

        // (apple OR banana) hits cards 2,3; intersected with column "In Progress" (2,3).
        CollectionAssert.AreEqual(new[] { 2L, 3L }, fixture.Search("(apple OR banana) AND column:\"In Progress\""));

        // ...while column "To Do" holds cards 1,4 — the intersection is empty.
        Assert.AreEqual(0, fixture.Search("(apple OR banana) AND column:\"To Do\"").Count);
    }

    [TestMethod]
    public void LikeWildcardsInUserTextMatchLiterally()
    {
        using var fixture = new SearchFixture();
        CollectionAssert.AreEqual(new[] { 4L }, fixture.Search("100%"));
        CollectionAssert.AreEqual(new[] { 4L }, fixture.Search("a_b"));
    }

    [TestMethod]
    public void CompiledPredicateUsesNamedParameters()
    {
        CompiledSearch compiled = SearchSqlCompiler.Compile(new SearchNode.FreeText("x"));

        StringAssert.Contains(compiled.Predicate, "$p0");
        StringAssert.Contains(compiled.Predicate, "$p1");
        Assert.AreEqual(2, compiled.Parameters.Count);
        Assert.AreEqual("%x%", compiled.Parameters[0].Value);
        Assert.AreEqual("%x%", compiled.Parameters[1].Value);
    }
}
