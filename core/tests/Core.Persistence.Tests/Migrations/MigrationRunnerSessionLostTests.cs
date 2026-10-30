using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Migrations;

/// <summary>
/// The session that goes away under the run (#364) ends it with an outcome that names where, and never with an exception nobody handles:
/// in the middle of a step it is the failure of that step, and while the registry is read it is the connection that did not hold.
/// </summary>
[Trait("Category", "Database")]
public sealed class MigrationRunnerSessionLostTests
{
    private const string Sleeping = "SELECT pg_sleep(60)";
    private const string RegistryRead = "ORDER BY version";

    private static IReadOnlyList<MigrationStep> StepsLosingTheSessionAfterOne() => TestSteps.OnTopOfTheEmbedded(
        ("creates-before", "CREATE TABLE zqx_before (a integer)"),
        ("loses-the-session", Sleeping),
        ("creates-after", "CREATE TABLE zqx_after (a integer)"));

    [DatabaseFact]
    public async Task SessionLostInTheMiddleOfAStepEndsTheRunAsStepFailedNamingItAndTheServerState()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = StepsLosingTheSessionAfterOne();

        var result = await LosingTheSessionAsync(database, steps);

        Assert.Equal(MigrationOutcome.StepFailed, result.Outcome);
        Assert.Equal(steps[^2].Name, result.Step);
        Assert.Equal(PostgresErrorCodes.AdminShutdown, result.SqlState);
    }

    [DatabaseFact]
    public async Task SessionLostInTheMiddleOfAStepReportsTheStepsBeforeItAndWritesNoRowBecauseTheSessionIsGone()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = StepsLosingTheSessionAfterOne();

        var result = await LosingTheSessionAsync(database, steps);

        Assert.Equal(steps.Take(steps.Count - 2).Select(step => step.Name), result.Applied);
        Assert.Equal(steps[^3].Version, result.Found);
        Assert.Equal(steps[^1].Version, result.Expected);
        Assert.False(result.FailureRecorded);
        Assert.Empty(await database.ReadFailuresAsync());
    }

    [DatabaseFact]
    public async Task SessionLostInTheMiddleOfAStepLeavesTheStepsBeforeItAndNoneAfterIt()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = StepsLosingTheSessionAfterOne();

        _ = await LosingTheSessionAsync(database, steps);

        Assert.True(await database.TableExistsAsync("zqx_before"));
        Assert.False(await database.TableExistsAsync("zqx_after"));
        Assert.Equal(steps.Take(steps.Count - 2).Select(step => step.Name), (await database.ReadRegistryAsync()).Select(row => row.Name));
    }

    [DatabaseFact]
    public async Task SessionLostWhileTheRegistryIsReadEndsTheRunAsNotConnectedWithTheServerState()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = TestSteps.OnTopOfTheEmbedded(("pending", "CREATE TABLE zqx_pending (a integer)"));
        _ = await database.MigrateAsync([.. steps.Take(steps.Count - 1)]);
        await using var holder = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var transaction = await holder.BeginTransactionAsync(TestContext.Current.CancellationToken);
        Assert.Equal("public.schema_version", SchemaRegistry.Table);
        await using var hold = new NpgsqlCommand("LOCK TABLE public.schema_version IN ACCESS EXCLUSIVE MODE", holder, transaction);
        _ = await hold.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        var migrating = database.MigrateAsync(steps);
        await database.TerminateSessionRunningAsync(RegistryRead);
        var result = await migrating;

        Assert.Equal(MigrationOutcome.NotConnected, result.Outcome);
        Assert.Equal(PostgresErrorCodes.AdminShutdown, result.SqlState);
        Assert.Empty(result.Applied);
    }

    private static async Task<MigrationResult> LosingTheSessionAsync(ScratchDatabase database, IReadOnlyList<MigrationStep> steps)
    {
        var migrating = database.MigrateAsync(steps);
        await database.TerminateSessionRunningAsync(Sleeping);
        return await migrating;
    }
}
