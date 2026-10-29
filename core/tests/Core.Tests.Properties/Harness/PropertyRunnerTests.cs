using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Tests.Properties.Harness;

/// <summary>The runner prints the seed of a failure, replays it from the seed variable, declares its cases and fails past its deadline.</summary>
public sealed class PropertyRunnerTests
{
    private static readonly PropertyBudget Budget = new(Cases: 500, Deadline: TimeSpan.FromSeconds(30));

    private static readonly Gen<List<int>> Lists = Gen.Int.List[0, 12];

    private static PropertyRunner RunnerWithSeed(string? seed) =>
        new(name => name == PropertyRunner.SeedVariable ? seed : null, Budget, _ => { });

    private static void FailsOnTwoMarkedIntegers(IEnumerable<int> values) => Assert.True(values.Count(value => Math.Abs(value % 7) == 3) < 2);

    [Fact]
    public void FailureNamesTheSeedThatReplaysIt()
    {
        var failure = Assert.Throws<CsCheckException>(() => RunnerWithSeed(null).Check(Lists, FailsOnTwoMarkedIntegers));

        Assert.Contains(PropertyRunner.SeedVariable + "=", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SameSeedReproducesTheSameFailure()
    {
        var first = Assert.Throws<CsCheckException>(() => RunnerWithSeed(null).Check(Lists, FailsOnTwoMarkedIntegers));
        var seed = FailedSample.SeedIn(first.Message);

        var replay = Assert.Throws<CsCheckException>(() => RunnerWithSeed(seed).Check(Lists, FailsOnTwoMarkedIntegers));

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
    public void TimeInPlaceOfCasesIsRefused()
    {
        var runner = new PropertyRunner(name => name == PropertyRunner.TimeVariable ? "1" : null, Budget, _ => { });

        _ = Assert.Throws<InvalidOperationException>(() => runner.Check(Gen.Int, _ => { }));
    }

    [Fact]
    public void RunPastItsDeadlineFails()
    {
        var budget = new PropertyBudget(Cases: 1000, Deadline: TimeSpan.FromMilliseconds(200));
        var slow = new PropertyRunner(_ => null, budget, _ => { });

        _ = Assert.Throws<TimeoutException>(() => slow.Check(Gen.Int, _ => SpinWait.SpinUntil(() => false, TimeSpan.FromMilliseconds(50))));
    }
}
