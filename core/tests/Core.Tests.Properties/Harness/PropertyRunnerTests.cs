using CsCheck;

namespace Core.Tests.Properties.Harness;

/// <summary>The runner prints the seed of a failure, replays it from the seed variable, declares its cases and fails past its deadline.</summary>
public sealed class PropertyRunnerTests
{
    private static readonly PropertyBudget Budget = new(Cases: 500, Deadline: TimeSpan.FromSeconds(30));

    private static PropertyRunner RunnerWithSeed(string? seed) =>
        new(name => name == PropertyRunner.SeedVariable ? seed : null, Budget, _ => { });

    private static void FailsOnSomeIntegers(int value) => Assert.True(value % 7 != 3);

    [Fact]
    public void FailureNamesTheSeedThatReplaysIt()
    {
        var failure = Assert.Throws<CsCheckException>(() => RunnerWithSeed(null).Check(Gen.Int, FailsOnSomeIntegers));

        Assert.Contains(PropertyRunner.SeedVariable + "=", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SameSeedReproducesTheSameFailure()
    {
        var first = Assert.Throws<CsCheckException>(() => RunnerWithSeed(null).Check(Gen.Int, FailsOnSomeIntegers));
        var seed = FailedSample.SeedIn(first.Message);

        var replay = Assert.Throws<CsCheckException>(() => RunnerWithSeed(seed).Check(Gen.Int, FailsOnSomeIntegers));

        Assert.Equal(seed, FailedSample.SeedIn(replay.Message));
        Assert.Equal(FailedSample.CounterexampleIn(first.Message), FailedSample.CounterexampleIn(replay.Message));
    }

    [Fact]
    public void ReportsTheDeclaredNumberOfCases()
    {
        var reports = new List<string>();
        var runner = new PropertyRunner(_ => null, Budget, reports.Add);

        runner.Check(Gen.Int, _ => { });

        Assert.Equal($"Passed {Budget.Cases} iterations.", Assert.Single(reports));
    }

    [Fact]
    public void RunPastItsDeadlineFails()
    {
        var budget = new PropertyBudget(Cases: 1000, Deadline: TimeSpan.FromMilliseconds(200));
        var slow = new PropertyRunner(_ => null, budget, _ => { });

        _ = Assert.Throws<TimeoutException>(() => slow.Check(Gen.Int, _ => Thread.Sleep(TimeSpan.FromMilliseconds(50))));
    }
}
