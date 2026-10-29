using Core.Persistence.Migrations;
using Npgsql;

namespace Core.Tests.Integration.Fixtures;

/// <summary>
/// A database of its own on the shared server, made the way tools/database makes cdg: owned by the role owner, open to app alone,
/// with pgaudit installed and the public schema handed to owner. A migration test runs here, so that the registry it writes is
/// never seen by another test; the database is dropped on dispose. Connections are not pooled, so the drop finds none open.
/// </summary>
internal sealed class ScratchDatabase : IAsyncDisposable
{
    private readonly string superuserConnectionString;
    private readonly string name;

    private ScratchDatabase(string superuserConnectionString, string name, string ownerConnectionString, string appConnectionString)
    {
        this.superuserConnectionString = superuserConnectionString;
        this.name = name;
        OwnerConnectionString = ownerConnectionString;
        AppConnectionString = appConnectionString;
    }

    /// <summary>The connection string of the role that owns the database, pointing at the scratch database.</summary>
    public string OwnerConnectionString { get; }

    /// <summary>The connection string of the application role, pointing at the scratch database.</summary>
    public string AppConnectionString { get; }

    public static async Task<ScratchDatabase> CreateAsync()
    {
        var shared = await PostgresFixture.SharedAsync();
        var name = $"zqx_mig_{Guid.NewGuid():N}";
        await ExecuteAsync(shared.SuperuserConnectionString, $"CREATE DATABASE {name} OWNER owner");
        await ExecuteAsync(shared.SuperuserConnectionString, $"REVOKE ALL ON DATABASE {name} FROM PUBLIC");
        await ExecuteAsync(shared.SuperuserConnectionString, $"GRANT CONNECT ON DATABASE {name} TO app");
        await ExecuteAsync(
            Unpooled(shared.SuperuserConnectionString, name),
            "CREATE EXTENSION pgaudit",
            "ALTER SCHEMA public OWNER TO owner",
            "REVOKE ALL ON SCHEMA public FROM PUBLIC",
            "GRANT USAGE ON SCHEMA public TO app");
        return new ScratchDatabase(shared.SuperuserConnectionString, name, Unpooled(shared.OwnerConnectionString, name), Unpooled(shared.AppConnectionString, name));
    }

    /// <summary>One run of the migration over these steps, as the role owner.</summary>
    public async Task<MigrationResult> MigrateAsync(IReadOnlyList<MigrationStep> steps)
    {
        await using var dataSource = NpgsqlDataSource.Create(OwnerConnectionString);
        return await new MigrationRunner(dataSource, steps).RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Takes back the read the migration gave PUBLIC on the registry, as the role owner.</summary>
    public Task RevokeRegistryReadAsync() =>
        ExecuteAsync(OwnerConnectionString, "REVOKE SELECT ON public.schema_version FROM PUBLIC");

    /// <summary>Whether a table of that name exists in the public schema.</summary>
    public async Task<bool> TableExistsAsync(string table)
    {
        await using var connection = await Sql.OpenAsync(OwnerConnectionString);
        await using var command = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL", connection);
        _ = command.Parameters.AddWithValue("name", $"public.{table}");
        return (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>The rows of the registry in order of version; the registry must exist.</summary>
    public async Task<IReadOnlyList<RegistryRow>> ReadRegistryAsync()
    {
        await using var connection = await Sql.OpenAsync(OwnerConnectionString);
        await using var command = new NpgsqlCommand("SELECT version, name, sha256, applied_at, applied_by FROM public.schema_version ORDER BY version", connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<RegistryRow>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var appliedAt = await reader.GetFieldValueAsync<DateTimeOffset>(3, TestContext.Current.CancellationToken);
            rows.Add(new RegistryRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), appliedAt, reader.GetString(4)));
        }

        return rows;
    }

    /// <summary>The rows of the failure table in order of id; the table must exist.</summary>
    public async Task<IReadOnlyList<FailureRow>> ReadFailuresAsync()
    {
        await using var connection = await Sql.OpenAsync(OwnerConnectionString);
        await using var command = new NpgsqlCommand("SELECT version, name, sha256, sqlstate, failed_at, failed_by FROM public.schema_failure ORDER BY id", connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<FailureRow>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var failedAt = await reader.GetFieldValueAsync<DateTimeOffset>(4, TestContext.Current.CancellationToken);
            var sqlState = await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : reader.GetString(3);
            rows.Add(new FailureRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), sqlState, failedAt, reader.GetString(5)));
        }

        return rows;
    }

    /// <summary>The column names of a table of the public schema, in order of position.</summary>
    public async Task<IReadOnlyList<string>> ColumnsOfAsync(string table)
    {
        await using var connection = await Sql.OpenAsync(OwnerConnectionString);
        await using var command = new NpgsqlCommand(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table ORDER BY ordinal_position", connection);
        _ = command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var columns = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    public async ValueTask DisposeAsync() =>
        await ExecuteAsync(superuserConnectionString, $"DROP DATABASE IF EXISTS {name} WITH (FORCE)");

    private static string Unpooled(string connectionString, string database) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = database, Pooling = false }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, params string[] statements)
    {
        await using var connection = await Sql.OpenAsync(connectionString);
        await using var batch = new NpgsqlBatch(connection);
        foreach (var statement in statements)
        {
            batch.BatchCommands.Add(new NpgsqlBatchCommand(statement));
        }

        _ = await batch.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>One row of <c>public.schema_failure</c>, without its identity.</summary>
    internal sealed record FailureRow(int Version, string Name, string Sha256, string? SqlState, DateTimeOffset FailedAt, string FailedBy);

    /// <summary>One row of <c>public.schema_version</c>.</summary>
    internal sealed record RegistryRow(int Version, string Name, string Sha256, DateTimeOffset AppliedAt, string AppliedBy);
}
