using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Tests.Integration.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Migrations;

/// <summary>
/// The lock the runner takes belongs to the server session, and a pooled session that is not reset outlives the run: the run must give the lock
/// back, on every way it can end, or the next run through the same pool is the only one that can ever migrate.
/// </summary>
[Trait("Category", "Database")]
public sealed class MigrationRunnerLockTests
{
    private const string HeldAdvisoryLocks =
        "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND database = (SELECT oid FROM pg_database WHERE datname = current_database())";

    [DatabaseFact]
    public async Task NoLockIsHeldAfterTheStepsAreApplied()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await using var dataSource = NpgsqlDataSource.Create(Pooled(database));
        _ = await new MigrationRunner(dataSource, EmbeddedMigrations.Load()).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, await HeldLocksAsync(dataSource));
    }

    [DatabaseFact]
    public async Task NoLockIsHeldAfterARefusal()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await using var dataSource = NpgsqlDataSource.Create(Pooled(database));
        var steps = TestSteps.OnTopOfTheEmbedded(("creates-ahead", "CREATE TABLE zqx_ahead (a integer)"));
        _ = await new MigrationRunner(dataSource, steps).RunAsync(TestContext.Current.CancellationToken);

        var refused = await new MigrationRunner(dataSource, EmbeddedMigrations.Load()).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MigrationOutcome.RefusedAhead, refused.Outcome);
        Assert.Equal(0, await HeldLocksAsync(dataSource));
    }

    [DatabaseFact]
    public async Task NoLockIsHeldAfterAStepFails()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await using var dataSource = NpgsqlDataSource.Create(Pooled(database));
        var steps = TestSteps.OnTopOfTheEmbedded(("fails", "SELECT 1 / 0"));

        var failed = await new MigrationRunner(dataSource, steps).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MigrationOutcome.StepFailed, failed.Outcome);
        Assert.Equal(0, await HeldLocksAsync(dataSource));
    }

    private static string Pooled(ScratchDatabase database) =>
        new NpgsqlConnectionStringBuilder(database.OwnerConnectionString) { Pooling = true, NoResetOnClose = true }.ConnectionString;

    /// <summary>How many advisory locks any session holds in this database; the pooled session that ran the migration is still open.</summary>
    private static async Task<long> HeldLocksAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(HeldAdvisoryLocks, connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
