namespace YKanBan.Storage.Workspace;

/// <summary>
/// Schema definitions for the per-workspace database. The board is a single
/// board with a columns → cards hierarchy. All data validation is expressed
/// in DDL (CHECK / UNIQUE / FOREIGN KEY) so that any row entering the
/// database is valid by construction; cascading deletes are covered by the
/// same DDL.
/// </summary>
public static class WorkspaceSchema
{
    /// <summary>Latest workspace schema version this build understands.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Ordered, contiguous migration list for the workspace database.</summary>
    public static readonly IReadOnlyList<SchemaMigration> Migrations = new SchemaMigration[]
    {
        new SchemaMigration(1, "initial schema", """
            CREATE TABLE columns (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                title       TEXT    NOT NULL UNIQUE CHECK (length(title) > 0),
                description TEXT    NOT NULL DEFAULT '',
                created_at  INTEGER NOT NULL,
                updated_at  INTEGER NOT NULL
            );

            CREATE TABLE cards (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                column_id   INTEGER NOT NULL REFERENCES columns (id) ON DELETE CASCADE,
                title       TEXT    NOT NULL DEFAULT '',
                content     TEXT    NOT NULL DEFAULT '',
                created_at  INTEGER NOT NULL,
                updated_at  INTEGER NOT NULL
            );

            CREATE TABLE tags (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                name        TEXT    NOT NULL UNIQUE CHECK (length(name) > 0),
                color       TEXT    NOT NULL CHECK (color GLOB '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
                description TEXT    NOT NULL DEFAULT ''
            );

            CREATE TABLE card_tags (
                card_id INTEGER NOT NULL REFERENCES cards (id) ON DELETE CASCADE,
                tag_id  INTEGER NOT NULL REFERENCES tags  (id) ON DELETE CASCADE,
                PRIMARY KEY (card_id, tag_id)
            );
            """),
    };
}
