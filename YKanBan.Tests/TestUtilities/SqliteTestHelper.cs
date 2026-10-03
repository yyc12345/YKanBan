using Microsoft.Data.Sqlite;
using YKanBan.Storage;
using YKanBan.Storage.Workspace;

namespace YKanBan.Tests.TestUtilities;

/// <summary>
/// Shared helpers for the storage tests: small SQL conveniences plus opening a
/// workspace database with the real migration list.
/// </summary>
public static class SqliteTestHelper
{
    /// <summary>
    /// Executes a non-query SQL statement and returns the affected row count.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <param name="sql">The statement to execute.</param>
    /// <param name="parameters">Name/value pairs bound as SQL parameters.</param>
    /// <returns>The number of affected rows.</returns>
    public static int Exec(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        return command.ExecuteNonQuery();
    }

    /// <summary>
    /// Returns the scalar result of a SQL query as a string.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <param name="sql">The query to execute.</param>
    /// <returns>The scalar result cast to a string.</returns>
    public static string ScalarString(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Returns the scalar result of a SQL query as a long.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <param name="sql">The query to execute.</param>
    /// <returns>The scalar result cast to a long.</returns>
    public static long ScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Returns whether a table with the given name exists in the database.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <param name="tableName">The table name to look for.</param>
    /// <returns><see langword="true"/> when the table exists.</returns>
    public static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", tableName);
        return (long)command.ExecuteScalar()! == 1;
    }

    /// <summary>
    /// Returns the titles of all columns, ordered by id ascending.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <returns>The column titles in id order.</returns>
    public static List<string> GetColumnTitles(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT title FROM columns ORDER BY id ASC;";
        using var reader = command.ExecuteReader();

        var titles = new List<string>();
        while (reader.Read())
        {
            titles.Add(reader.GetString(0));
        }
        return titles;
    }

    /// <summary>
    /// Returns the descriptions of all columns, ordered by id ascending.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <returns>The column descriptions in id order.</returns>
    public static List<string> GetColumnDescriptions(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT description FROM columns ORDER BY id ASC;";
        using var reader = command.ExecuteReader();

        var descriptions = new List<string>();
        while (reader.Read())
        {
            descriptions.Add(reader.GetString(0));
        }
        return descriptions;
    }

    /// <summary>
    /// Returns all tags as name/color/description tuples, ordered by id ascending.
    /// </summary>
    /// <param name="connection">An open database connection.</param>
    /// <returns>The tags in id order.</returns>
    public static List<(string Name, string Color, string Description)> GetTags(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, color, description FROM tags ORDER BY id ASC;";
        using var reader = command.ExecuteReader();

        var tags = new List<(string, string, string)>();
        while (reader.Read())
        {
            tags.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }
        return tags;
    }

    /// <summary>
    /// Opens a workspace database with the real workspace migration list.
    /// </summary>
    /// <param name="folderPath">The workspace folder path.</param>
    /// <returns>An open, migrated connection.</returns>
    public static SqliteConnection OpenWorkspace(string folderPath)
    {
        return SqliteDatabase.Open(WorkspacePaths.Database(folderPath), WorkspaceSchema.Migrations);
    }
}
