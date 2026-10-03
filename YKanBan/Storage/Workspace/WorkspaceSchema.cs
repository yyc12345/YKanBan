namespace YKanBan.Storage.Workspace;

/// <summary>
/// Schema definitions for the per-workspace database. A workspace holds a
/// single board with a columns → cards hierarchy plus tags and their
/// assignments. Data validity is expressed in DDL (CHECK / UNIQUE / FOREIGN
/// KEY) so every stored row is valid by construction, and cascading deletes
/// are covered by the same DDL.
///
/// Title and name uniqueness is NOCASE (ASCII case folding) while the columns
/// themselves keep the default BINARY collation, so ordering by title stays
/// code-point based. Until the first release the v1 script may be edited in
/// place; after it, every schema change must be appended as a new migration.
/// </summary>
public static class WorkspaceSchema
{
    /// <summary>
    /// Newest workspace schema version this build understands.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Ordered, contiguous migration list for the workspace database.
    /// </summary>
    public static readonly IReadOnlyList<SchemaMigration> Migrations =
    [
        new SchemaMigration(CurrentVersion, "initial schema", """
            CREATE TABLE columns (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                title       TEXT    NOT NULL CHECK (length(title) > 0),
                description TEXT    NOT NULL DEFAULT '',
                created_at  INTEGER NOT NULL,
                updated_at  INTEGER NOT NULL,
                UNIQUE (title COLLATE NOCASE)
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
                name        TEXT    NOT NULL CHECK (length(name) > 0),
                color       TEXT    NOT NULL CHECK (color GLOB '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
                description TEXT    NOT NULL DEFAULT '',
                UNIQUE (name COLLATE NOCASE)
            );

            CREATE TABLE card_tags (
                card_id INTEGER NOT NULL REFERENCES cards (id) ON DELETE CASCADE,
                tag_id  INTEGER NOT NULL REFERENCES tags  (id) ON DELETE CASCADE,
                PRIMARY KEY (card_id, tag_id)
            );
            """),
    ];
}
