using Microsoft.Data.Sqlite;

using YKanBan.Models;
using YKanBan.Search;

namespace YKanBan.Storage.Workspace;

/// <summary>
/// Data access for one open workspace database: CRUD for columns, cards and
/// tags, card moves and tag assignment. Cascading deletes are left to the DDL.
///
/// Timestamp rules:
///  - cards.updated_at refreshes only when a card edit is confirmed with
///    changes; moving a card to another column never refreshes it;
///  - columns.updated_at refreshes only when title or description changes;
///  - a confirmation with no effective change performs no write at all.
/// </summary>
public sealed class WorkspaceRepository : IDisposable {
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Opens the repository over an initialized workspace database.
    /// </summary>
    /// <param name="folderPath">The workspace folder.</param>
    public WorkspaceRepository(string folderPath) {
        _connection = SqliteDatabase.Open(WorkspacePaths.Database(folderPath), WorkspaceSchema.Migrations);
    }

    /// <summary>
    /// Closes the underlying connection.
    /// </summary>
    public void Dispose() => _connection.Dispose();

    #region Columns

    /// <summary>
    /// Returns all columns ordered by id ascending.
    /// </summary>
    /// <returns>All columns in id order.</returns>
    public IReadOnlyList<ColumnModel> GetColumns() {
        using var command = CreateCommand(
            "SELECT id, title, description, created_at, updated_at FROM columns ORDER BY id ASC;");
        using var reader = command.ExecuteReader();

        var columns = new List<ColumnModel>();
        while (reader.Read()) {
            columns.Add(ReadColumn(reader));
        }
        return columns;
    }

    /// <summary>
    /// Returns the column with the given id.
    /// </summary>
    /// <param name="columnId">The column id.</param>
    /// <returns>The column, or <see langword="null"/> when it does not exist.</returns>
    public ColumnModel? GetColumn(long columnId) {
        using var command = CreateCommand(
            "SELECT id, title, description, created_at, updated_at FROM columns WHERE id = $id;");
        command.Parameters.AddWithValue("$id", columnId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadColumn(reader) : null;
    }

    /// <summary>
    /// Creates a column with created_at equal to updated_at.
    /// </summary>
    /// <param name="title">The non-empty, table-unique title.</param>
    /// <param name="description">The optional description.</param>
    /// <returns>The created column.</returns>
    /// <exception cref="SqliteException">The title is empty or not unique.</exception>
    public ColumnModel AddColumn(string title, string description) {
        long now = UnixTime.Now;
        using var transaction = _connection.BeginTransaction();
        long id = ExecuteInsert(transaction,
            "INSERT INTO columns (title, description, created_at, updated_at) VALUES ($title, $description, $now, $now);",
            ("$title", title), ("$description", description), ("$now", now));
        transaction.Commit();

        return new ColumnModel(id, title, description, now, now);
    }

    /// <summary>
    /// Rewrites the title and description. A call with no effective change
    /// writes nothing and leaves the timestamp alone.
    /// </summary>
    /// <param name="columnId">The column to update.</param>
    /// <param name="title">The new non-empty, table-unique title.</param>
    /// <param name="description">The new description.</param>
    /// <returns>The updated column, or the unchanged one for a no-op.</returns>
    /// <exception cref="InvalidOperationException">The column does not exist.</exception>
    /// <exception cref="SqliteException">The new title is empty or not unique.</exception>
    public ColumnModel UpdateColumn(long columnId, string title, string description) {
        ColumnModel existing = GetColumn(columnId)
            ?? throw new InvalidOperationException($"Column {columnId} does not exist.");

        // Unchanged title and description: no write, no timestamp refresh.
        if (existing.Title == title && existing.Description == description) {
            return existing;
        }

        long now = UnixTime.Now;
        using var command = CreateCommand(
            "UPDATE columns SET title = $title, description = $description, updated_at = $now WHERE id = $id;");
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$now", now);
        command.Parameters.AddWithValue("$id", columnId);
        command.ExecuteNonQuery();

        return new ColumnModel(columnId, title, description, existing.CreatedAt, now);
    }

    /// <summary>
    /// Deletes a column; the DDL cascade removes its cards and their tag links.
    /// </summary>
    /// <param name="columnId">The column to delete.</param>
    /// <returns><see langword="true"/> when a row was deleted.</returns>
    public bool DeleteColumn(long columnId) => ExecuteDelete("DELETE FROM columns WHERE id = $id;", columnId);

    #endregion

    #region Cards

    /// <summary>
    /// Returns all cards with their assigned tags loaded, ordered by id ascending.
    /// </summary>
    /// <returns>All cards in id order.</returns>
    public IReadOnlyList<CardModel> GetCards() {
        using var command = CreateCommand("""
            SELECT c.id, c.column_id, c.title, c.content, c.created_at, c.updated_at,
                   t.id, t.name, t.color, t.description
            FROM cards c
            LEFT JOIN card_tags ct ON ct.card_id = c.id
            LEFT JOIN tags t ON t.id = ct.tag_id
            ORDER BY c.id ASC;
            """);
        using var reader = command.ExecuteReader();

        // Single grouped pass: one output card per distinct card id, with its tags accumulated alongside.
        var cards = new List<CardModel>();
        var tagsByCard = new Dictionary<long, List<TagModel>>();
        while (reader.Read()) {
            long cardId = reader.GetInt64(0);
            if (!tagsByCard.TryGetValue(cardId, out List<TagModel>? tags)) {
                tags = new List<TagModel>();
                tagsByCard[cardId] = tags;
                cards.Add(new CardModel(
                    cardId,
                    reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt64(4),
                    reader.GetInt64(5),
                    tags));
            }

            if (!reader.IsDBNull(6)) {
                tags.Add(ReadTag(reader, 6));
            }
        }
        return cards;
    }

    /// <summary>
    /// Returns the ids of the cards matching a compiled search predicate.
    /// </summary>
    /// <param name="search">The compiled search predicate and its parameters.</param>
    /// <returns>The matching card ids.</returns>
    public IReadOnlyCollection<long> GetCardIdsMatching(CompiledSearch search) {
        using var command = CreateCommand($"SELECT c.id FROM cards c WHERE {search.Predicate};");
        foreach ((string name, object value) in search.Parameters) {
            command.Parameters.AddWithValue(name, value);
        }
        using var reader = command.ExecuteReader();

        var ids = new HashSet<long>();
        while (reader.Read()) {
            ids.Add(reader.GetInt64(0));
        }
        return ids;
    }

    /// <summary>
    /// Returns the card with the given id, with its tags loaded.
    /// </summary>
    /// <param name="cardId">The card id.</param>
    /// <returns>The card, or <see langword="null"/> when it does not exist.</returns>
    public CardModel? GetCard(long cardId) {
        using var command = CreateCommand("""
            SELECT c.id, c.column_id, c.title, c.content, c.created_at, c.updated_at,
                   t.id, t.name, t.color, t.description
            FROM cards c
            LEFT JOIN card_tags ct ON ct.card_id = c.id
            LEFT JOIN tags t ON t.id = ct.tag_id
            WHERE c.id = $id
            ORDER BY c.id ASC;
            """);
        command.Parameters.AddWithValue("$id", cardId);
        using var reader = command.ExecuteReader();

        // The LEFT JOIN may repeat the card row once per tag; build it on the first row only.
        CardModel? card = null;
        var tags = new List<TagModel>();
        while (reader.Read()) {
            card ??= new CardModel(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                tags);

            if (!reader.IsDBNull(6)) {
                tags.Add(ReadTag(reader, 6));
            }
        }
        return card;
    }

    /// <summary>
    /// Creates a card with its initial tag assignment and created_at equal to updated_at.
    /// </summary>
    /// <param name="columnId">The owning column.</param>
    /// <param name="title">The card title, possibly empty.</param>
    /// <param name="content">The card content, possibly empty.</param>
    /// <param name="tagIds">The tags to assign initially.</param>
    /// <returns>The created card with tags loaded.</returns>
    /// <exception cref="SqliteException">The column or a tag id does not exist.</exception>
    public CardModel AddCard(long columnId, string title, string content, IReadOnlyCollection<long> tagIds) {
        long now = UnixTime.Now;
        using var transaction = _connection.BeginTransaction();
        long id = ExecuteInsert(transaction,
            "INSERT INTO cards (column_id, title, content, created_at, updated_at) VALUES ($columnId, $title, $content, $now, $now);",
            ("$columnId", columnId), ("$title", title), ("$content", content), ("$now", now));
        foreach (long tagId in tagIds.Distinct()) {
            ExecuteNonQuery(transaction,
                "INSERT INTO card_tags (card_id, tag_id) VALUES ($cardId, $tagId);",
                ("$cardId", id), ("$tagId", tagId));
        }
        transaction.Commit();

        return new CardModel(id, columnId, title, content, now, now, LoadTags(id));
    }

    /// <summary>
    /// Applies one card edit confirmation as a single atomic unit: the card row
    /// (title, content, refreshed updated_at) plus the complete tag assignment.
    /// A confirmation with no effective change writes nothing; a failure
    /// mid-way rolls the whole edit back.
    /// </summary>
    /// <param name="cardId">The card to update.</param>
    /// <param name="title">The new title.</param>
    /// <param name="content">The new content.</param>
    /// <param name="tagIds">The complete set of tags to assign.</param>
    /// <returns>The updated card with tags loaded, or the unchanged one for a no-op.</returns>
    /// <exception cref="InvalidOperationException">The card does not exist.</exception>
    /// <exception cref="SqliteException">A tag id does not exist; nothing is written.</exception>
    public CardModel UpdateCard(long cardId, string title, string content, IReadOnlyCollection<long> tagIds) {
        CardModel existing = GetCard(cardId)
            ?? throw new InvalidOperationException($"Card {cardId} does not exist.");

        // No effective change in title, content or tag set: no write, no timestamp refresh.
        bool tagsChanged = !tagIds.Distinct().ToHashSet().SetEquals(existing.Tags.Select(tag => tag.Id));
        if (existing.Title == title && existing.Content == content && !tagsChanged) {
            return existing;
        }

        // The confirmed write refreshes updated_at even when only the tag set changed.
        long now = UnixTime.Now;
        using var transaction = _connection.BeginTransaction();
        ExecuteNonQuery(transaction,
            "UPDATE cards SET title = $title, content = $content, updated_at = $now WHERE id = $id;",
            ("$title", title), ("$content", content), ("$now", now), ("$id", cardId));

        // Tag assignment is rewritten wholesale; card_tags carries nothing beyond the pair.
        ExecuteNonQuery(transaction, "DELETE FROM card_tags WHERE card_id = $id;", ("$id", cardId));
        foreach (long tagId in tagIds.Distinct()) {
            ExecuteNonQuery(transaction,
                "INSERT INTO card_tags (card_id, tag_id) VALUES ($cardId, $tagId);",
                ("$cardId", cardId), ("$tagId", tagId));
        }
        transaction.Commit();

        return new CardModel(cardId, existing.ColumnId, title, content, existing.CreatedAt, now, LoadTags(cardId));
    }

    /// <summary>
    /// Moves a card to another column. This writes to the database but
    /// deliberately does NOT refresh the card's updated_at; moving to the
    /// current column is a no-op.
    /// </summary>
    /// <param name="cardId">The card to move.</param>
    /// <param name="targetColumnId">The destination column.</param>
    /// <returns><see langword="true"/> when the card actually moved.</returns>
    public bool MoveCard(long cardId, long targetColumnId) {
        using var command = CreateCommand(
            "UPDATE cards SET column_id = $columnId WHERE id = $id AND column_id <> $columnId;");
        command.Parameters.AddWithValue("$columnId", targetColumnId);
        command.Parameters.AddWithValue("$id", cardId);
        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Deletes a card; the DDL cascade removes its tag links.
    /// </summary>
    /// <param name="cardId">The card to delete.</param>
    /// <returns><see langword="true"/> when a row was deleted.</returns>
    public bool DeleteCard(long cardId) => ExecuteDelete("DELETE FROM cards WHERE id = $id;", cardId);

    #endregion

    #region Tags

    /// <summary>
    /// Returns all tags ordered by id ascending; unreferenced tags are included.
    /// </summary>
    /// <returns>All tags in id order.</returns>
    public IReadOnlyList<TagModel> GetTags() {
        using var command = CreateCommand(
            "SELECT id, name, color, description FROM tags ORDER BY id ASC;");
        using var reader = command.ExecuteReader();

        var tags = new List<TagModel>();
        while (reader.Read()) {
            tags.Add(ReadTag(reader, 0));
        }
        return tags;
    }

    /// <summary>
    /// Returns every tag together with the number of cards using it, ordered by tag id.
    /// </summary>
    /// <returns>The tag/usage pairs in tag id order.</returns>
    public IReadOnlyList<(TagModel Tag, long UsageCount)> GetTagsWithUsage() {
        using var command = CreateCommand("""
            SELECT t.id, t.name, t.color, t.description, COUNT(ct.card_id)
            FROM tags t
            LEFT JOIN card_tags ct ON ct.tag_id = t.id
            GROUP BY t.id, t.name, t.color, t.description
            ORDER BY t.id ASC;
            """);
        using var reader = command.ExecuteReader();

        var tags = new List<(TagModel Tag, long UsageCount)>();
        while (reader.Read()) {
            tags.Add((ReadTag(reader, 0), reader.GetInt64(4)));
        }
        return tags;
    }

    /// <summary>
    /// Creates a tag.
    /// </summary>
    /// <param name="name">The non-empty, table-unique name.</param>
    /// <param name="color">The tag color.</param>
    /// <param name="description">The optional description.</param>
    /// <returns>The created tag.</returns>
    /// <exception cref="SqliteException">The name is empty or not unique.</exception>
    public TagModel AddTag(string name, RgbColor color, string description) {
        using var transaction = _connection.BeginTransaction();
        long id = ExecuteInsert(transaction,
            "INSERT INTO tags (name, color, description) VALUES ($name, $color, $description);",
            ("$name", name), ("$color", color.ToString()), ("$description", description));
        transaction.Commit();

        return new TagModel(id, name, color, description);
    }

    /// <summary>
    /// Rewrites name, color and description. A call with no effective change writes nothing.
    /// </summary>
    /// <param name="tagId">The tag to update.</param>
    /// <param name="name">The new non-empty, table-unique name.</param>
    /// <param name="color">The new color.</param>
    /// <param name="description">The new description.</param>
    /// <returns>The updated tag, or the unchanged one for a no-op.</returns>
    /// <exception cref="InvalidOperationException">The tag does not exist.</exception>
    /// <exception cref="SqliteException">The new name is empty or not unique.</exception>
    public TagModel UpdateTag(long tagId, string name, RgbColor color, string description) {
        TagModel existing = GetTag(tagId)
            ?? throw new InvalidOperationException($"Tag {tagId} does not exist.");

        // Unchanged name, color and description: no write.
        if (existing.Name == name && existing.Color == color && existing.Description == description) {
            return existing;
        }

        using var command = CreateCommand(
            "UPDATE tags SET name = $name, color = $color, description = $description WHERE id = $id;");
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$color", color.ToString());
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$id", tagId);
        command.ExecuteNonQuery();

        return new TagModel(tagId, name, color, description);
    }

    /// <summary>
    /// Deletes a tag; the DDL cascade removes its card links while the cards survive.
    /// </summary>
    /// <param name="tagId">The tag to delete.</param>
    /// <returns><see langword="true"/> when a row was deleted.</returns>
    public bool DeleteTag(long tagId) => ExecuteDelete("DELETE FROM tags WHERE id = $id;", tagId);

    #endregion

    #region Helpers

    /// <summary>
    /// Returns the tag with the given id.
    /// </summary>
    /// <param name="tagId">The tag id.</param>
    /// <returns>The tag, or <see langword="null"/> when it does not exist.</returns>
    private TagModel? GetTag(long tagId) {
        using var command = CreateCommand(
            "SELECT id, name, color, description FROM tags WHERE id = $id;");
        command.Parameters.AddWithValue("$id", tagId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTag(reader, 0) : null;
    }

    /// <summary>
    /// Returns the tags currently assigned to a card, ordered by tag id.
    /// </summary>
    /// <param name="cardId">The card id.</param>
    /// <returns>The assigned tags in id order.</returns>
    private IReadOnlyList<TagModel> LoadTags(long cardId) {
        using var command = CreateCommand("""
            SELECT t.id, t.name, t.color, t.description
            FROM tags t
            JOIN card_tags ct ON ct.tag_id = t.id
            WHERE ct.card_id = $cardId
            ORDER BY t.id ASC;
            """);
        command.Parameters.AddWithValue("$cardId", cardId);
        using var reader = command.ExecuteReader();

        var tags = new List<TagModel>();
        while (reader.Read()) {
            tags.Add(ReadTag(reader, 0));
        }
        return tags;
    }

    /// <summary>
    /// Reads a columns row from the reader cursor.
    /// </summary>
    /// <param name="reader">The reader positioned on a columns row.</param>
    /// <returns>The column model.</returns>
    private static ColumnModel ReadColumn(SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3), reader.GetInt64(4));

    /// <summary>
    /// Reads a tags row from the reader cursor, starting at the given column
    /// offset; the color text is converted to its components.
    /// </summary>
    /// <param name="reader">The reader positioned on a row containing a tag.</param>
    /// <param name="offset">Index of the tag's id column.</param>
    /// <returns>The tag model.</returns>
    private static TagModel ReadTag(SqliteDataReader reader, int offset) =>
        new(reader.GetInt64(offset), reader.GetString(offset + 1), RgbColor.Parse(reader.GetString(offset + 2)), reader.GetString(offset + 3));

    /// <summary>
    /// Creates a command bound to this connection.
    /// </summary>
    /// <param name="sql">The command text.</param>
    /// <param name="transaction">The transaction to enlist in, when any.</param>
    /// <returns>The prepared command.</returns>
    private SqliteCommand CreateCommand(string sql, SqliteTransaction? transaction = null) {
        SqliteCommand command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    /// <summary>
    /// Executes a parameterized non-query statement inside a transaction.
    /// </summary>
    /// <param name="transaction">The transaction to enlist in.</param>
    /// <param name="sql">The statement to execute.</param>
    /// <param name="parameters">Name/value pairs bound as SQL parameters.</param>
    private void ExecuteNonQuery(SqliteTransaction transaction, string sql, params (string Name, object? Value)[] parameters) {
        using SqliteCommand command = CreateCommand(sql, transaction);
        foreach ((string name, object? value) in parameters) {
            command.Parameters.AddWithValue(name, value);
        }
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts a row inside a transaction and returns the generated rowid.
    /// </summary>
    /// <param name="transaction">The transaction to enlist in.</param>
    /// <param name="sql">The INSERT statement.</param>
    /// <param name="parameters">Name/value pairs bound as SQL parameters.</param>
    /// <returns>The rowid assigned to the inserted row.</returns>
    private long ExecuteInsert(SqliteTransaction transaction, string sql, params (string Name, object? Value)[] parameters) {
        ExecuteNonQuery(transaction, sql, parameters);
        using SqliteCommand rowId = CreateCommand("SELECT last_insert_rowid();", transaction);
        return (long)rowId.ExecuteScalar()!;
    }

    /// <summary>
    /// Deletes a row by id.
    /// </summary>
    /// <param name="sql">The DELETE statement with an <c>$id</c> parameter.</param>
    /// <param name="id">The id to delete.</param>
    /// <returns><see langword="true"/> when a row was deleted.</returns>
    private bool ExecuteDelete(string sql, long id) {
        using SqliteCommand command = CreateCommand(sql);
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    #endregion
}
