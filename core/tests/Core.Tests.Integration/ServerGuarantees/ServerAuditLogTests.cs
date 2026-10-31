using Core.Persistence.Tests.Fixtures;
using Core.Tests.Integration.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.ServerGuarantees;

/// <summary>
/// The image's server records, in a log of its own, what the application does in the database, and records none of the material
/// it handles: not in a statement, not in an error (issue #60, CA-1 and CA-3). Each test about the absence has a twin that turns
/// off the setting responsible and shows the material in the log, so that a sweep over a log that cannot hold it never passes.
/// </summary>
[Trait("Category", "Database")]
public sealed class ServerAuditLogTests
{
    [DatabaseFact]
    public async Task WriteAndReadOfTheApplicationAreInTheServerLogWithoutTheApplicationWritingAnEvent()
    {
        await using var table = await AuditedTable.CreateAsync();

        await table.InsertAsync(id: 1);
        await table.ReadAsync();
        await table.ReadTheEndAsync();
        var log = await ServerLog.UntilContainsAsync(table.EndName);

        _ = Assert.Single(ServerLog.AuditLines(log, "app", "WRITE", "INSERT", table.Name));
        _ = Assert.Single(ServerLog.AuditLines(log, "app", "READ", "SELECT", table.Name));
    }

    [DatabaseFact]
    public async Task StatementTheServerRefusesIsInTheServerLogToo()
    {
        await using var table = await AuditedTable.CreateAsync();
        await table.InsertAsync(id: 1);

        await table.InsertRefusedAsUniqueAsync(id: 2);
        await table.ReadTheEndAsync();
        var log = await ServerLog.UntilContainsAsync(table.EndName);

        Assert.Equal(2, ServerLog.AuditLines(log, "app", "WRITE", "INSERT", table.Name).Count);
    }

    [DatabaseFact]
    public async Task SeededMaterialIsInNoLineOfTheServerLog()
    {
        await using var table = await AuditedTable.CreateAsync();
        await table.InsertAsync(id: 1);
        await table.ReadAsync();
        await table.InsertRefusedAsUniqueAsync(id: 2);
        await table.InsertRefusedAsNullAsync(id: 3);
        await table.ReadTheEndAsync();

        var log = await ServerLog.UntilContainsAsync(table.EndName);

        Assert.DoesNotContain(table.Marker, log, StringComparison.Ordinal);
        Assert.Equal(3, ServerLog.AuditLines(log, "app", "WRITE", "INSERT", table.Name).Count);
    }

    [DatabaseFact]
    public async Task ParametersWouldBeInTheServerLogIfTheAuditLoggedThem()
    {
        var marker = $"ZQX-SEED-{Guid.NewGuid():N}";
        await using var connection = await OpenUnpooledAsSuperuserAsync();
        await RunAsync(connection, "SET pgaudit.log_parameter = on");

        await RunAsync(connection, "SELECT @marker::text", marker);
        var log = await ServerLog.UntilContainsAsync(marker);

        Assert.Contains(marker, log, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DetailOfAnErrorWouldBeInTheServerLogIfErrorsWereLogged()
    {
        var marker = $"ZQX-SEED-{Guid.NewGuid():N}";
        await using var connection = await OpenUnpooledAsSuperuserAsync();
        await RunAsync(connection, "SET log_min_messages = warning");
        await RunAsync(connection, "CREATE TEMP TABLE zqx_seed (body text UNIQUE)");
        await RunAsync(connection, "INSERT INTO zqx_seed VALUES (@marker)", marker);

        _ = await Assert.ThrowsAsync<PostgresException>(() => RunAsync(connection, "INSERT INTO zqx_seed VALUES (@marker)", marker));
        var log = await ServerLog.UntilContainsAsync($"Key (body)=({marker}) already exists");

        Assert.Contains(marker, log, StringComparison.Ordinal);
    }

    private static async Task<NpgsqlConnection> OpenUnpooledAsSuperuserAsync()
    {
        var database = await PostgresFixture.SharedAsync();
        return await Sql.OpenAsync(new NpgsqlConnectionStringBuilder(database.SuperuserConnectionString) { Pooling = false }.ConnectionString);
    }

    private static async Task RunAsync(NpgsqlConnection connection, string sql, string? marker = null)
    {
        await using var batch = new NpgsqlBatch(connection);
        var command = new NpgsqlBatchCommand(sql);
        if (marker is not null)
        {
            _ = command.Parameters.AddWithValue("marker", marker);
        }

        batch.BatchCommands.Add(command);
        _ = await batch.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
