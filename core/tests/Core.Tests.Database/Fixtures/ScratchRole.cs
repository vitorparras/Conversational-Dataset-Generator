using Npgsql;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// A role made for one test, that can connect to the scratch database and use its public schema and holds nothing else: what a role the deployment adds later, or one the owner
/// forgot, is until someone grants it something. The role is dropped on dispose, with what it was granted in the database.
/// </summary>
internal sealed class ScratchRole : IAsyncDisposable
{
    private readonly string superuserConnectionString;
    private readonly string role;
    private readonly string database;

    private ScratchRole(string superuserConnectionString, string role, string database, string connectionString)
    {
        this.superuserConnectionString = superuserConnectionString;
        this.role = role;
        this.database = database;
        ConnectionString = connectionString;
    }

    /// <summary>The connection string of the role, pointing at the scratch database.</summary>
    public string ConnectionString { get; }

    /// <summary>Makes the role over the database and gives it the right to connect and to use the public schema, and nothing more.</summary>
    internal static async Task<ScratchRole> CreateAsync(string superuserConnectionString, string database)
    {
        var role = $"zqx_bare_{Guid.NewGuid():N}";
        var password = $"{Guid.NewGuid():N}";
        await ExecuteAsync(
            superuserConnectionString,
            $"CREATE ROLE {role} LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS",
            $"GRANT CONNECT ON DATABASE {database} TO {role}");
        await ExecuteAsync(InDatabase(superuserConnectionString, database), $"GRANT USAGE ON SCHEMA public TO {role}");
        var connection = new NpgsqlConnectionStringBuilder(superuserConnectionString) { Database = database, Username = role, Password = password, Pooling = false };
        return new ScratchRole(superuserConnectionString, role, database, connection.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await ExecuteAsync(InDatabase(superuserConnectionString, database), $"DROP OWNED BY {role}");
        await ExecuteAsync(superuserConnectionString, $"DROP ROLE {role}");
    }

    private static string InDatabase(string connectionString, string database) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = database, Pooling = false }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, params string[] statements)
    {
        await using var connection = await Sql.OpenAsync(connectionString);
        await using var batch = new NpgsqlBatch(connection);
        foreach (var statement in statements)
        {
            // nosemgrep: sql-concatenation-csharp interpolates the names of a role and a database this class made itself, never input.
            batch.BatchCommands.Add(new NpgsqlBatchCommand(statement));
        }

        _ = await batch.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
