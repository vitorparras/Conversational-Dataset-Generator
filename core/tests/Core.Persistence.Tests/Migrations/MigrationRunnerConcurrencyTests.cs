using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Tests.Integration.Fixtures;

namespace Core.Persistence.Tests.Migrations;

/// <summary>Several migrations started at once on an empty database: the first applies every step, the others find nothing to do.</summary>
[Trait("Category", "Database")]
public sealed class MigrationRunnerConcurrencyTests
{
    private const int Runners = 6;

    /// <summary>The slow step keeps the lock held long enough for every other runner to be waiting on it.</summary>
    private static IReadOnlyList<MigrationStep> SlowSteps() => TestSteps.OnTopOfTheEmbedded(
        ("creates-once", "SELECT pg_sleep(1); CREATE TABLE zqx_once (a integer)"),
        ("creates-after", "CREATE TABLE zqx_after (a integer)"));

    [DatabaseFact]
    public async Task OnlyOneOfTheSimultaneousRunsAppliesTheSteps()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = SlowSteps();

        var results = await Task.WhenAll(Enumerable.Range(0, Runners).Select(_ => database.MigrateAsync(steps)));

        Assert.Equal(
            Enumerable.Repeat(MigrationOutcome.NothingToDo, Runners - 1).Prepend(MigrationOutcome.Applied),
            results.Select(result => result.Outcome).Order());
    }

    [DatabaseFact]
    public async Task EachStepIsRegisteredOnceAfterSimultaneousRuns()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = SlowSteps();

        _ = await Task.WhenAll(Enumerable.Range(0, Runners).Select(_ => database.MigrateAsync(steps)));

        Assert.Equal(steps.Select(step => step.Name), (await database.ReadRegistryAsync()).Select(row => row.Name));
    }
}
