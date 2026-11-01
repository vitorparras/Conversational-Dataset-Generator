using Core.Persistence.Tests.Fixtures;
using Npgsql;
using NpgsqlTypes;

namespace Core.Tests.Integration.Fixtures;

/// <summary>
/// A table of material in a database of its own, which the application's role reads and writes as the application does, with
/// parameters; the material is a marker no other test shares. The table that ends a run is read last, so that its line in the
/// server log shows every line before it already written.
/// </summary>
internal sealed class AuditedTable : IAsyncDisposable
{
    private const string UniqueViolation = "23505";
    private const string NotNullViolation = "23502";
    private readonly ScratchDatabase database;
    private readonly string suffix = $"{Guid.NewGuid():N}";

    private AuditedTable(ScratchDatabase database)
    {
        this.database = database;
        Name = $"zqx_material_{suffix}";
        Marker = $"ZQX-SEED-{Guid.NewGuid():N}";
    }

    /// <summary>The table the application writes and reads.</summary>
    public string Name { get; }

    /// <summary>The table the application reads last.</summary>
    public string EndName => $"zqx_ending_{suffix}";

    /// <summary>The material the test seeds: it must reach the table and no line of the server log.</summary>
    public string Marker { get; }

    public static async Task<AuditedTable> CreateAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        var table = new AuditedTable(database);
        await database.RunAsOwnerAsync(
            $"CREATE TABLE {table.Name} (id integer PRIMARY KEY, body text NOT NULL UNIQUE)",
            $"CREATE TABLE {table.EndName} (id integer PRIMARY KEY)",
            $"GRANT SELECT, INSERT ON {table.Name}, {table.EndName} TO app");
        return table;
    }

    /// <summary>The application stores the material.</summary>
    public Task InsertAsync(int id) => RunAsync(InsertSql, IdParameter(id), BodyParameter(Marker));

    /// <summary>The application looks the material up.</summary>
    public Task ReadAsync() => RunAsync($"SELECT id FROM {Name} WHERE body = @body", BodyParameter(Marker));

    /// <summary>The application stores the material again, under another id, and the server refuses: the error names the key.</summary>
    public async Task InsertRefusedAsUniqueAsync(int id) =>
        Assert.Equal(UniqueViolation, (await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(id))).SqlState);

    /// <summary>The application stores a row without the material and the server refuses: the error names the row.</summary>
    public async Task InsertRefusedAsNullAsync(int id) =>
        Assert.Equal(NotNullViolation, (await Assert.ThrowsAsync<PostgresException>(() => RunAsync(InsertSql, IdParameter(id), BodyParameter(null)))).SqlState);

    /// <summary>The application ends the run with the last read.</summary>
    public Task ReadTheEndAsync() => RunAsync($"SELECT id FROM {EndName} WHERE id = @id", IdParameter(0));

    public async ValueTask DisposeAsync() => await database.DisposeAsync();

    private string InsertSql => $"INSERT INTO {Name} (id, body) VALUES (@id, @body)";

    private static NpgsqlParameter IdParameter(int id) => new("id", id);

    private static NpgsqlParameter BodyParameter(string? body) => new("body", NpgsqlDbType.Text) { Value = (object?)body ?? DBNull.Value };

    private async Task RunAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var batch = new NpgsqlBatch(connection);
        var command = new NpgsqlBatchCommand(sql);
        foreach (var parameter in parameters)
        {
            _ = command.Parameters.Add(parameter);
        }

        batch.BatchCommands.Add(command);
        _ = await batch.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
