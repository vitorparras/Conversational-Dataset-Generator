using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// Operators and types resolve through the search path, and nothing in the text of a statement qualifies an operator. So every reading of the
/// guarantees runs with the path set to the catalog, whatever the database set, and gives it back when the statement is done.
/// </summary>
[Trait("Category", "Database")]
public sealed class SearchPathPinTests
{
    private const string Setting = "SHOW search_path";

    [DatabaseFact]
    public async Task ReadingRunsWithTheSearchPathOnTheCatalogWhateverTheDatabaseSet()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await using var connection = await OpenWithSearchPathAheadAsync(database);

        var seen = await ReadAsync(connection);

        Assert.Equal("pg_catalog, pg_temp", seen);
    }

    [DatabaseFact]
    public async Task ReadingInsideATransactionTheCheckOpenedStillRunsWithTheSearchPathOnTheCatalog()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await using var connection = await OpenWithSearchPathAheadAsync(database);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var seen = await ReadAsync(connection);

        Assert.Equal("pg_catalog, pg_temp", seen);
    }

    [DatabaseFact]
    public async Task SearchPathOfTheSessionIsNotLeftChangedAfterTheReading()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await using var connection = await OpenWithSearchPathAheadAsync(database);
        _ = await ReadAsync(connection);

        await using var command = new NpgsqlCommand(Setting, connection);
        var after = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal("zqx_shadow, pg_catalog", after);
    }

    private static async Task<NpgsqlConnection> OpenWithSearchPathAheadAsync(ScratchDatabase database)
    {
        await database.RunAsOwnerAsync("CREATE SCHEMA zqx_shadow");
        var name = new NpgsqlConnectionStringBuilder(database.OwnerConnectionString).Database;
        await database.RunAsOwnerAsync($"ALTER DATABASE {name} SET search_path = zqx_shadow, pg_catalog");
        var connection = new NpgsqlConnection(database.AppConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static async Task<string> ReadAsync(NpgsqlConnection connection)
    {
        var seen = string.Empty;
        _ = await ServerProbe.ReadRowAsync(
            connection, Setting, row => Capture(row, value => seen = value), TestContext.Current.CancellationToken);
        return seen;
    }

    private static Finding Capture(System.Data.IDataRecord row, Action<string> keep)
    {
        keep((string)row[0]);
        return Finding.Holds("lido");
    }
}
