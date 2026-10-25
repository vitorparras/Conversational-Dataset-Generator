using System.Data.Common;
using System.Text;

internal static class Sample
{
    internal static void Find(DbDataSource source, DbConnection connection, DbCommand command, string name)
    {
        // ruleid: sql-concatenation-csharp
        command.CommandText = "SELECT id FROM person WHERE name = '" + name + "'";
        // ruleid: sql-concatenation-csharp
        command.CommandText = $"SELECT id FROM person WHERE name = '{name}'";
        var query = string.Format("SELECT id FROM {0}", name);
        // ruleid: sql-concatenation-csharp
        using var other = new SqliteCommand(query, connection);
        // ruleid: sql-concatenation-csharp
        using var created = source.CreateCommand("DELETE FROM person WHERE name = '" + name + "'");
        // ruleid: sql-concatenation-csharp
        using var initialized = new SqliteCommand { CommandText = $"DELETE FROM person WHERE name = '{name}'" };
        var builder = new StringBuilder("SELECT id FROM person WHERE name = '");
        builder.Append(name);
        // ruleid: sql-concatenation-csharp
        command.CommandText = builder.ToString();
        // ok: sql-concatenation-csharp
        command.CommandText = "SELECT id FROM person WHERE name = @name";
        // ok: sql-concatenation-csharp
        using var parameterized = source.CreateCommand("SELECT id FROM person WHERE name = @name");
        // ok: sql-concatenation-csharp
        using var safe = new SqliteCommand("SELECT id FROM person WHERE name = @name", connection);
    }
}
