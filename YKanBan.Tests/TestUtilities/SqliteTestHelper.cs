using Microsoft.Data.Sqlite;
using YKanBan.Storage;
using YKanBan.Storage.Workspace;

namespace YKanBan.Tests.TestUtilities;

/// <summary>Shared helpers for storage tests.</summary>
public static class SqliteTestHelper
{
    /// <summary>Executes a non-query SQL statement and returns the affected row count.</summary>
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

    /// <summary>Returns the scalar result of a SQL query as a string.</summary>
    public static string ScalarString(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)command.ExecuteScalar()!;
    }

    /// <summary>Returns the scalar result of a SQL query as a long.</summary>
    public static long ScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>Returns true when a table with the given name exists.</summary>
    public static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", tableName);
        return (long)command.ExecuteScalar()! == 1;
    }

    /// <summary>Returns the titles of all columns, ordered by id.</summary>
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

    /// <summary>Opens a workspace database with the real migration list.</summary>
    public static SqliteConnection OpenWorkspace(string folderPath)
    {
        return SqliteDatabase.Open(WorkspacePaths.Database(folderPath), WorkspaceSchema.Migrations);
    }
}
