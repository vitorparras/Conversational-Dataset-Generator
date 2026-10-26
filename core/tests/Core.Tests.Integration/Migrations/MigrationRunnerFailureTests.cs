using Core.Persistence.Migrations;
using Core.Tests.Integration.Fixtures;

namespace Core.Tests.Integration.Migrations;

/// <summary>A step that fails in the middle of a set of test steps (CA-3): it leaves nothing, the steps before it stay, the ones after it do not run.</summary>
[Trait("Category", "Database")]
public sealed class MigrationRunnerFailureTests
{
    private static IReadOnlyList<MigrationStep> StepsWithAFailureInTheMiddle() => TestSteps.OnTopOfTheEmbedded(
        ("creates-before", "CREATE TABLE zqx_before (a integer)"),
        ("fails-after-creating", "CREATE TABLE zqx_partial (a integer); INSERT INTO zqx_partial VALUES (1); SELECT 1 / 0"),
        ("creates-after", "CREATE TABLE zqx_after (a integer)"));

    [DatabaseFact]
    public async Task FailingStepEndsTheRunAsStepFailedNamingTheStepAndTheServerState()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = StepsWithAFailureInTheMiddle();

        var result = await database.MigrateAsync(steps);

        Assert.Equal(MigrationOutcome.StepFailed, result.Outcome);
        Assert.Equal(steps[2].Name, result.Step);
        Assert.Equal("22012", result.SqlState);
    }

    [DatabaseFact]
    public async Task FailingStepReportsTheStepsBeforeItAsAppliedAndTheVersionReached()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = StepsWithAFailureInTheMiddle();

        var result = await database.MigrateAsync(steps);

        Assert.Equal(steps.Take(2).Select(step => step.Name), result.Applied);
        Assert.Equal(steps[1].Version, result.Found);
        Assert.Equal(steps[^1].Version, result.Expected);
    }

    [DatabaseFact]
    public async Task FailingStepLeavesNothingOfItselfInTheSchema()
    {
        await using var database = await ScratchDatabase.CreateAsync();

        _ = await database.MigrateAsync(StepsWithAFailureInTheMiddle());

        Assert.False(await database.TableExistsAsync("zqx_partial"));
    }

    [DatabaseFact]
    public async Task FailingStepLeavesNoRowOfItselfInTheRegistry()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = StepsWithAFailureInTheMiddle();

        _ = await database.MigrateAsync(steps);

        var rows = await database.ReadRegistryAsync();
        Assert.Equal(steps.Take(2).Select(step => step.Name), rows.Select(row => row.Name));
        Assert.DoesNotContain(rows, row => row.Name == steps[2].Name);
    }

    [DatabaseFact]
    public async Task StepsBeforeTheFailureStayApplied()
    {
        await using var database = await ScratchDatabase.CreateAsync();

        _ = await database.MigrateAsync(StepsWithAFailureInTheMiddle());

        Assert.True(await database.TableExistsAsync("zqx_before"));
    }

    [DatabaseFact]
    public async Task StepsAfterTheFailureDoNotRun()
    {
        await using var database = await ScratchDatabase.CreateAsync();

        _ = await database.MigrateAsync(StepsWithAFailureInTheMiddle());

        Assert.False(await database.TableExistsAsync("zqx_after"));
    }

    [DatabaseFact]
    public async Task RunWithTheStepCorrectedAppliesOnlyWhatWasPending()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var failing = StepsWithAFailureInTheMiddle();
        _ = await database.MigrateAsync(failing);
        var corrected = TestSteps.Following(
            [.. failing.Take(2)],
            ("fixed", "CREATE TABLE zqx_partial (a integer)"),
            ("creates-after", "CREATE TABLE zqx_after (a integer)"));

        var result = await database.MigrateAsync(corrected);

        Assert.Equal(MigrationOutcome.Applied, result.Outcome);
        Assert.Equal(corrected.Skip(2).Select(step => step.Name), result.Applied);
        Assert.Equal(corrected.Select(step => step.Name), (await database.ReadRegistryAsync()).Select(row => row.Name));
    }
}
