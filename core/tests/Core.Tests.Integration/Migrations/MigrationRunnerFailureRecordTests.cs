using Core.Persistence.Migrations;
using Core.Tests.Integration.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.Migrations;

/// <summary>
/// The failure of a step is written to <c>schema_failure</c> outside the transaction that was undone (#340): only the columns of the table,
/// only added to, and never a change to how the run ends.
/// </summary>
[Trait("Category", "Database")]
public sealed class MigrationRunnerFailureRecordTests
{
    private const string Failing = "CREATE TABLE zqx_partial (a integer); INSERT INTO zqx_partial VALUES (1); SELECT 1 / 0";

    [DatabaseFact]
    public async Task FailingStepLeavesARowWithItsVersionNameDigestAndServerState()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = TestSteps.OnTopOfTheEmbedded(("fails", Failing));
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var result = await database.MigrateAsync(steps);

        var failure = Assert.Single(await database.ReadFailuresAsync());
        Assert.Equal((steps[^1].Version, steps[^1].Name, steps[^1].Sha256, "22012", "owner"), (failure.Version, failure.Name, failure.Sha256, failure.SqlState, failure.FailedBy));
        Assert.InRange(failure.FailedAt, before, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.True(result.FailureRecorded);
    }

    [DatabaseFact]
    public async Task FailureRowDoesNotUndoTheRollbackOfTheStep()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = TestSteps.OnTopOfTheEmbedded(("fails", Failing));

        _ = await database.MigrateAsync(steps);

        Assert.False(await database.TableExistsAsync("zqx_partial"));
        Assert.Equal(steps.Take(steps.Count - 1).Select(step => step.Name), (await database.ReadRegistryAsync()).Select(row => row.Name));
    }

    [DatabaseFact]
    public async Task FailureTableHoldsNoColumnForAMessageOrForTheTextOfTheStep()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());

        var columns = await database.ColumnsOfAsync("schema_failure");

        Assert.Equal(["id", "version", "name", "sha256", "sqlstate", "failed_at", "failed_by"], columns);
    }

    [DatabaseFact]
    public async Task FailureRowCarriesNothingOfTheServerMessageOrTheStep()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = TestSteps.OnTopOfTheEmbedded(("fails", "CREATE TABLE zqx_secret_table (a integer); SELECT 'zqx-material-8841'::integer"));

        _ = await database.MigrateAsync(steps);

        var failure = Assert.Single(await database.ReadFailuresAsync());
        Assert.Equal("22P02", failure.SqlState);
        Assert.DoesNotContain("zqx", string.Join('|', failure.Name, failure.Sha256, failure.SqlState, failure.FailedBy), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TwoFailuresOfTheSameStepLeaveTwoRows()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = TestSteps.OnTopOfTheEmbedded(("fails", Failing));
        _ = await database.MigrateAsync(steps);

        var second = await database.MigrateAsync(steps);

        var failures = await database.ReadFailuresAsync();
        Assert.True(second.FailureRecorded);
        Assert.Equal([steps[^1].Name, steps[^1].Name], failures.Select(failure => failure.Name));
    }

    [DatabaseFact]
    public async Task StepCorrectedAfterAFailureIsAppliedAndTheEarlierFailuresStay()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var failing = TestSteps.OnTopOfTheEmbedded(("fails", Failing));
        _ = await database.MigrateAsync(failing);
        _ = await database.MigrateAsync(failing);
        var corrected = TestSteps.Following(failing.Take(failing.Count - 1), ("fixed", "CREATE TABLE zqx_partial (a integer)"));

        var result = await database.MigrateAsync(corrected);

        Assert.Equal(MigrationOutcome.Applied, result.Outcome);
        Assert.False(result.FailureRecorded);
        Assert.Equal([corrected[^1].Name], result.Applied);
        Assert.Equal(2, (await database.ReadFailuresAsync()).Count);
    }

    [DatabaseFact]
    public async Task FailureLeavesTheRegistryAndTheRerunReadsOnlyIt()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var failing = TestSteps.OnTopOfTheEmbedded(("fails", Failing));
        _ = await database.MigrateAsync(failing);
        var registered = await database.ReadRegistryAsync();

        var result = await database.MigrateAsync([.. failing.Take(failing.Count - 1)]);

        Assert.Equal(MigrationOutcome.NothingToDo, result.Outcome);
        Assert.Equal(registered, await database.ReadRegistryAsync());
    }

    [DatabaseFact]
    public async Task FailureOfTheFirstStepIsNotRecordedBecauseTheTableIsNotThereYet()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        IReadOnlyList<MigrationStep> steps = [MigrationStep.Of(1, "0001-fails", "SELECT 1 / 0"u8.ToArray())];

        var result = await database.MigrateAsync(steps);

        Assert.Equal(MigrationOutcome.StepFailed, result.Outcome);
        Assert.Equal("22012", result.SqlState);
        Assert.False(result.FailureRecorded);
    }

    [DatabaseFact]
    public async Task FailureOfTheStepThatCreatesTheTableIsNotRecordedEither()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var embedded = EmbeddedMigrations.Load();
        var steps = new List<MigrationStep> { embedded[0], MigrationStep.Of(2, "0002-fails", "CREATE TABLE zqx_partial (a integer); SELECT 1 / 0"u8.ToArray()) };

        var result = await database.MigrateAsync(steps);

        Assert.Equal(MigrationOutcome.StepFailed, result.Outcome);
        Assert.Equal(steps[1].Name, result.Step);
        Assert.False(result.FailureRecorded);
        Assert.False(await database.TableExistsAsync("schema_failure"));
    }

    [DatabaseFact]
    public async Task InsertThatTheServerRefusesLeavesTheRunAsStepFailedWithTheFailureNotRecorded()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = TestSteps.OnTopOfTheEmbedded(
            ("refuses-rows", "ALTER TABLE public.schema_failure ADD CONSTRAINT zqx_never CHECK (false) NOT VALID"),
            ("fails", Failing));

        var result = await database.MigrateAsync(steps);

        Assert.Equal(MigrationOutcome.StepFailed, result.Outcome);
        Assert.Equal(steps[^1].Name, result.Step);
        Assert.Equal("22012", result.SqlState);
        Assert.False(result.FailureRecorded);
        Assert.Empty(await database.ReadFailuresAsync());
    }

    [DatabaseFact]
    public async Task ApplicationRoleReadsTheFailuresAndCannotWriteThem()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(TestSteps.OnTopOfTheEmbedded(("fails", Failing)));
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var read = new NpgsqlCommand("SELECT count(*) FROM public.schema_failure", connection);
        await using var forge = new NpgsqlCommand("INSERT INTO public.schema_failure (version, name, sha256) VALUES (99, 'forged', 'x')", connection);

        var count = await read.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        var refused = await Assert.ThrowsAsync<PostgresException>(() => forge.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1L, count);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }
}
