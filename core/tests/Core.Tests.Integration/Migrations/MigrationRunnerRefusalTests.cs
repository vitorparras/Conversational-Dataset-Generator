using Core.Persistence.Migrations;
using Core.Tests.Integration.Fixtures;

namespace Core.Tests.Integration.Migrations;

/// <summary>A registry that no longer matches the build (CA-4): a step applied with other content, or a step this build does not carry. Nothing runs.</summary>
[Trait("Category", "Database")]
public sealed class MigrationRunnerRefusalTests
{
    private static readonly (string Name, string Sql) Original = ("creates-table", "CREATE TABLE zqx_table (a integer)");
    private static readonly (string Name, string Sql) Altered = ("creates-table", "CREATE TABLE zqx_table (a integer, b integer)");
    private static readonly (string Name, string Sql) Pending = ("creates-pending", "CREATE TABLE zqx_pending (a integer)");

    [DatabaseFact]
    public async Task StepAppliedWithOtherContentIsRefusedAsDivergent()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var applied = TestSteps.OnTopOfTheEmbedded(Original);
        _ = await database.MigrateAsync(applied);
        var altered = TestSteps.OnTopOfTheEmbedded(Altered, Pending);

        var result = await database.MigrateAsync(altered);

        Assert.Equal(MigrationOutcome.RefusedDivergent, result.Outcome);
        Assert.Equal(applied[^1].Name, result.Step);
        Assert.Equal(applied[^1].Sha256, result.Registered);
        Assert.Equal(altered[^2].Sha256, result.Current);
    }

    [DatabaseFact]
    public async Task DivergentStepIsRefusedWithoutRunningAnythingPending()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var applied = TestSteps.OnTopOfTheEmbedded(Original);
        _ = await database.MigrateAsync(applied);
        var registered = await database.ReadRegistryAsync();

        var result = await database.MigrateAsync(TestSteps.OnTopOfTheEmbedded(Altered, Pending));

        Assert.Empty(result.Applied);
        Assert.False(await database.TableExistsAsync("zqx_pending"));
        Assert.Equal(registered, await database.ReadRegistryAsync());
    }

    [DatabaseFact]
    public async Task DivergentStepIsNotRunAgainOverTheSchema()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(TestSteps.OnTopOfTheEmbedded(Original));

        _ = await database.MigrateAsync(TestSteps.OnTopOfTheEmbedded(Altered));

        Assert.Equal(["a"], await ColumnsOfAsync(database));
    }

    [DatabaseFact]
    public async Task RegistryAheadOfTheBuildIsRefusedAndNothingRuns()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var ahead = TestSteps.OnTopOfTheEmbedded(Original, Pending);
        _ = await database.MigrateAsync(ahead);
        var registered = await database.ReadRegistryAsync();

        var result = await database.MigrateAsync([.. ahead.Take(ahead.Count - 1)]);

        Assert.Equal(MigrationOutcome.RefusedAhead, result.Outcome);
        Assert.Equal(ahead[^1].Version, result.Found);
        Assert.Equal(ahead[^2].Version, result.Expected);
        Assert.Empty(result.Applied);
        Assert.Equal(registered, await database.ReadRegistryAsync());
    }

    private static async Task<IReadOnlyList<string>> ColumnsOfAsync(ScratchDatabase database)
    {
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new Npgsql.NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_name = 'zqx_table'", connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var columns = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }
}
