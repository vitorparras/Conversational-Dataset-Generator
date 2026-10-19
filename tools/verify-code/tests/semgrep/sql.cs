using System.Data.Common;

internal static class Sample
{
    internal static void Find(DbConnection connection, DbCommand command, string name)
    {
        // ruleid: sql-concatenation-csharp
        command.CommandText = "SELECT id FROM person WHERE name = '" + name + "'";
        // ruleid: sql-concatenation-csharp
        command.CommandText = $"SELECT id FROM person WHERE name = '{name}'";
        var query = string.Format("SELECT id FROM {0}", name);
        // ruleid: sql-concatenation-csharp
        using var other = new SqliteCommand(query, connection);
        // ok: sql-concatenation-csharp
        command.CommandText = "SELECT id FROM person WHERE name = @name";
        // ok: sql-concatenation-csharp
        using var safe = new SqliteCommand("SELECT id FROM person WHERE name = @name", connection);
    }
}
