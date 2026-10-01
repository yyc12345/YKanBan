using Microsoft.Data.Sqlite;
using YKanBan.Models;
using YKanBan.Storage.AppData;

namespace YKanBan.Storage.Workspace;

/// <summary>
/// Data access for one open workspace database: CRUD for columns, cards and
/// tags, card moves and tag assignment, plus the write-through that
/// refreshes the workspace's last_edited_at in the global app.db after
/// every successful data write.
///
/// Timestamp rules:
///  - cards.updated_at refreshes only when a card edit is confirmed with
///    changes; moving a card to another column never refreshes it;
///  - columns.updated_at refreshes only when title or description changes;
///  - a confirmation with no effective change performs no write at all.
/// </summary>
public sealed class WorkspaceRepository : IDisposable
{
    private readonly string _folderPath;
    private readonly AppDatabase _appDatabase;
    private readonly SqliteConnection _connection;

    /// <summary>Opens the repository over an initialized workspace database.</summary>
    /// <param name="folderPath">Workspace folder; the exact value is used as the app.db registry key.</param>
    /// <param name="appDatabase">Global registry refreshed after every successful write.</param>
    public WorkspaceRepository(string folderPath, AppDatabase appDatabase)
    {
        _folderPath = folderPath;
        _appDatabase = appDatabase;
        _connection = SqliteDatabase.Open(WorkspacePaths.Database(folderPath), WorkspaceSchema.Migrations);
    }

    /// <summary>Closes the underlying connection.</summary>
    public void Dispose() => _connection.Dispose();

    #region Columns

    /// <summary>Returns all columns ordered by id.</summary>
    public IReadOnlyList<ColumnModel> GetColumns()
    {
        using var command = CreateCommand(
            "SELECT id, title, description, created_at, updated_at FROM columns ORDER BY id ASC;");
        using var reader = command.ExecuteReader();

        var columns = new List<ColumnModel>();
        while (reader.Read())
        {
            columns.Add(ReadColumn(reader));
        }
        return columns;
    }

    /// <summary>Returns the column with the given id, or null.</summary>
    public ColumnModel? GetColumn(long columnId)
    {
        using var command = CreateCommand(
            "SELECT id, title, description, created_at, updated_at FROM columns WHERE id = $id;");
        command.Parameters.AddWithValue("$id", columnId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadColumn(reader) : null;
    }

    /// <summary>Creates a column with created_at = updated_at.</summary>
    /// <exception cref="SqliteException">The title is empty or not unique.</exception>
    public ColumnModel AddColumn(string title, string description)
    {
        long now = UnixTime.Now;
        using var transaction = _connection.BeginTransaction();
        long id = ExecuteInsert(transaction,
            "INSERT INTO columns (title, description, created_at, updated_at) VALUES ($title, $description, $now, $now);",
            ("$title", title), ("$description", description), ("$now", now));
        transaction.Commit();

        TouchEdited();
        return new ColumnModel(id, title, description, now, now);
    }

    /// <summary>Rewrites title and description; a no-change call writes nothing.</summary>
    /// <exception cref="InvalidOperationException">The column does not exist.</exception>
    /// <exception cref="SqliteException">The new title is empty or not unique.</exception>
    public ColumnModel UpdateColumn(long columnId, string title, string description)
    {
        ColumnModel existing = GetColumn(columnId)
            ?? throw new InvalidOperationException($"Column {columnId} does not exist.");

        // Unchanged title and description: no write, no timestamps.
        if (existing.Title == title && existing.Description == description)
        {
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

        TouchEdited();
        return new ColumnModel(columnId, title, description, existing.CreatedAt, now);
    }

    /// <summary>Deletes a column; the DDL cascade removes its cards and their tag links. Returns true when a row was deleted.</summary>
    public bool DeleteColumn(long columnId) => ExecuteDelete("DELETE FROM columns WHERE id = $id;", columnId);

    #endregion

    #region Cards

    /// <summary>Returns all cards with their tags loaded, ordered by id.</summary>
    public IReadOnlyList<CardModel> GetCards()
    {
        using var command = CreateCommand("""
            SELECT c.id, c.column_id, c.title, c.content, c.created_at, c.updated_at,
                   t.id, t.name, t.color, t.description
            FROM cards c
            LEFT JOIN card_tags ct ON ct.card_id = c.id
            LEFT JOIN tags t ON t.id = ct.tag_id
            ORDER BY c.id ASC;
            """);
        using var reader = command.ExecuteReader();

        // Single grouped pass: one output card per distinct card id, its tags accumulated alongside.
        var cards = new List<CardModel>();
        var tagsByCard = new Dictionary<long, List<TagModel>>();
        while (reader.Read())
        {
            long cardId = reader.GetInt64(0);
            if (!tagsByCard.TryGetValue(cardId, out List<TagModel>? tags))
            {
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
            if (!reader.IsDBNull(6))
            {
                tags.Add(new TagModel(reader.GetInt64(6), reader.GetString(7), reader.GetString(8), reader.GetString(9)));
            }
        }
        return cards;
    }

    /// <summary>Returns the card with the given id (tags loaded), or null.</summary>
    public CardModel? GetCard(long cardId)
    {
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

        CardModel? card = null;
        var tags = new List<TagModel>();
        while (reader.Read())
        {
            if (card is null)
            {
                card = new CardModel(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt64(4),
                    reader.GetInt64(5),
                    tags);
            }
            if (!reader.IsDBNull(6))
            {
                tags.Add(new TagModel(reader.GetInt64(6), reader.GetString(7), reader.GetString(8), reader.GetString(9)));
            }
        }
        return card;
    }

    /// <summary>Creates a card with its initial tag assignment (created_at = updated_at).</summary>
    /// <exception cref="SqliteException">The column does not exist, or a tag id does not exist.</exception>
    public CardModel AddCard(long columnId, string title, string content, IReadOnlyCollection<long> tagIds)
    {
        long now = UnixTime.Now;
        using var transaction = _connection.BeginTransaction();
        long id = ExecuteInsert(transaction,
            "INSERT INTO cards (column_id, title, content, created_at, updated_at) VALUES ($columnId, $title, $content, $now, $now);",
            ("$columnId", columnId), ("$title", title), ("$content", content), ("$now", now));
        foreach (long tagId in tagIds.Distinct())
        {
            ExecuteNonQuery(transaction,
                "INSERT INTO card_tags (card_id, tag_id) VALUES ($cardId, $tagId);",
                ("$cardId", id), ("$tagId", tagId));
        }
        transaction.Commit();

        TouchEdited();
        return new CardModel(id, columnId, title, content, now, now, LoadTags(id));
    }

    /// <summary>
    /// Applies one card edit confirmation as a single atomic unit: the card
    /// row (title, content, refreshed updated_at) plus the complete tag
    /// assignment. A confirmation with no effective change writes nothing;
    /// a failure mid-way rolls the whole edit back.
    /// </summary>
    /// <exception cref="InvalidOperationException">The card does not exist.</exception>
    /// <exception cref="SqliteException">A tag id does not exist; nothing is written.</exception>
    public CardModel UpdateCard(long cardId, string title, string content, IReadOnlyCollection<long> tagIds)
    {
        CardModel existing = GetCard(cardId)
            ?? throw new InvalidOperationException($"Card {cardId} does not exist.");

        // No change in title, content or tag set: no write, no timestamps.
        bool tagsChanged = !tagIds.Distinct().ToHashSet().SetEquals(existing.Tags.Select(tag => tag.Id));
        if (existing.Title == title && existing.Content == content && !tagsChanged)
        {
            return existing;
        }

        long now = UnixTime.Now;
        using var transaction = _connection.BeginTransaction();
        // The confirm write refreshes updated_at even when only the tag set changed.
        ExecuteNonQuery(transaction,
            "UPDATE cards SET title = $title, content = $content, updated_at = $now WHERE id = $id;",
            ("$title", title), ("$content", content), ("$now", now), ("$id", cardId));
        // Tag assignment is rewritten wholesale; card_tags carries nothing beyond the pair.
        ExecuteNonQuery(transaction, "DELETE FROM card_tags WHERE card_id = $id;", ("$id", cardId));
        foreach (long tagId in tagIds.Distinct())
        {
            ExecuteNonQuery(transaction,
                "INSERT INTO card_tags (card_id, tag_id) VALUES ($cardId, $tagId);",
                ("$cardId", cardId), ("$tagId", tagId));
        }
        transaction.Commit();

        TouchEdited();
        return new CardModel(cardId, existing.ColumnId, title, content, existing.CreatedAt, now, LoadTags(cardId));
    }

    /// <summary>
    /// Moves a card to another column. This is a data write (refreshes the
    /// workspace last_edited_at) but deliberately does NOT refresh the
    /// card's updated_at; moving to the current column is a no-write no-op.
    /// Returns true when the card moved.
    /// </summary>
    public bool MoveCard(long cardId, long targetColumnId)
    {
        using var command = CreateCommand(
            "UPDATE cards SET column_id = $columnId WHERE id = $id AND column_id <> $columnId;");
        command.Parameters.AddWithValue("$columnId", targetColumnId);
        command.Parameters.AddWithValue("$id", cardId);
        bool moved = command.ExecuteNonQuery() > 0;
        if (moved)
        {
            TouchEdited();
        }
        return moved;
    }

    /// <summary>Deletes a card; the DDL cascade removes its tag links. Returns true when a row was deleted.</summary>
    public bool DeleteCard(long cardId) => ExecuteDelete("DELETE FROM cards WHERE id = $id;", cardId);

    #endregion

    #region Tags

    /// <summary>Returns all tags ordered by id; unreferenced tags are included.</summary>
    public IReadOnlyList<TagModel> GetTags()
    {
        using var command = CreateCommand(
            "SELECT id, name, color, description FROM tags ORDER BY id ASC;");
        using var reader = command.ExecuteReader();

        var tags = new List<TagModel>();
        while (reader.Read())
        {
            tags.Add(ReadTag(reader));
        }
        return tags;
    }

    /// <summary>Returns every tag together with the number of cards using it, ordered by id.</summary>
    public IReadOnlyList<(TagModel Tag, long UsageCount)> GetTagsWithUsage()
    {
        using var command = CreateCommand("""
            SELECT t.id, t.name, t.color, t.description, COUNT(ct.card_id)
            FROM tags t
            LEFT JOIN card_tags ct ON ct.tag_id = t.id
            GROUP BY t.id, t.name, t.color, t.description
            ORDER BY t.id ASC;
            """);
        using var reader = command.ExecuteReader();

        var tags = new List<(TagModel Tag, long UsageCount)>();
        while (reader.Read())
        {
            tags.Add((ReadTag(reader), reader.GetInt64(4)));
        }
        return tags;
    }

    /// <summary>Creates a tag.</summary>
    /// <exception cref="SqliteException">The name is empty/not unique, or the color is not #RRGGBB.</exception>
    public TagModel AddTag(string name, string color, string description)
    {
        using var transaction = _connection.BeginTransaction();
        long id = ExecuteInsert(transaction,
            "INSERT INTO tags (name, color, description) VALUES ($name, $color, $description);",
            ("$name", name), ("$color", color), ("$description", description));
        transaction.Commit();

        TouchEdited();
        return new TagModel(id, name, color, description);
    }

    /// <summary>Rewrites name, color and description; a no-change call writes nothing.</summary>
    /// <exception cref="InvalidOperationException">The tag does not exist.</exception>
    /// <exception cref="SqliteException">The new name is empty/not unique, or the color is not #RRGGBB.</exception>
    public TagModel UpdateTag(long tagId, string name, string color, string description)
    {
        TagModel existing = GetTag(tagId)
            ?? throw new InvalidOperationException($"Tag {tagId} does not exist.");

        // Unchanged name, color and description: no write.
        if (existing.Name == name && existing.Color == color && existing.Description == description)
        {
            return existing;
        }

        using var command = CreateCommand(
            "UPDATE tags SET name = $name, color = $color, description = $description WHERE id = $id;");
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$color", color);
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$id", tagId);
        command.ExecuteNonQuery();

        TouchEdited();
        return new TagModel(tagId, name, color, description);
    }

    /// <summary>Deletes a tag; the DDL cascade removes its card links while the cards survive. Returns true when a row was deleted.</summary>
    public bool DeleteTag(long tagId) => ExecuteDelete("DELETE FROM tags WHERE id = $id;", tagId);

    #endregion

    #region Helpers

    /// <summary>Returns the tag with the given id, or null.</summary>
    private TagModel? GetTag(long tagId)
    {
        using var command = CreateCommand(
            "SELECT id, name, color, description FROM tags WHERE id = $id;");
        command.Parameters.AddWithValue("$id", tagId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTag(reader) : null;
    }

    /// <summary>Returns the tags currently assigned to a card, ordered by id.</summary>
    private IReadOnlyList<TagModel> LoadTags(long cardId)
    {
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
        while (reader.Read())
        {
            tags.Add(ReadTag(reader));
        }
        return tags;
    }

    /// <summary>Reads a columns row from the reader cursor.</summary>
    private static ColumnModel ReadColumn(SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3), reader.GetInt64(4));

    /// <summary>Reads a tags row from the reader cursor.</summary>
    private static TagModel ReadTag(SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));

    /// <summary>Creates a command bound to this connection.</summary>
    private SqliteCommand CreateCommand(string sql, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    /// <summary>Executes a parameterized non-query statement inside a transaction.</summary>
    private void ExecuteNonQuery(SqliteTransaction transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using SqliteCommand command = CreateCommand(sql, transaction);
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        command.ExecuteNonQuery();
    }

    /// <summary>Inserts a row inside a transaction and returns the generated rowid.</summary>
    private long ExecuteInsert(SqliteTransaction transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        ExecuteNonQuery(transaction, sql, parameters);
        using SqliteCommand rowId = CreateCommand("SELECT last_insert_rowid();", transaction);
        return (long)rowId.ExecuteScalar()!;
    }

    /// <summary>Deletes by id; refreshes last_edited_at only when a row was actually removed.</summary>
    private bool ExecuteDelete(string sql, long id)
    {
        using SqliteCommand command = CreateCommand(sql);
        command.Parameters.AddWithValue("$id", id);
        if (command.ExecuteNonQuery() == 0)
        {
            return false;
        }
        TouchEdited();
        return true;
    }

    /// <summary>Mirrors a successful data write into the global registry.</summary>
    private void TouchEdited() => _appDatabase.TouchLastEdited(_folderPath);

    #endregion
}
