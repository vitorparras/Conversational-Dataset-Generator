using System.Text.RegularExpressions;
using CsCheck;
using Xunit.Sdk;

namespace Core.Tests.Properties;

/// <summary>
/// Decision 0007 and issue 41: a failing property reports its seed and smallest counterexample, the
/// same seed repeats the failure, and a property that does not fit its time fails.
/// </summary>
public sealed partial class PropertyHarnessTests
{
    // A property that is false on purpose: it fails for every value above ten.
    private static void AlwaysBelowEleven(int value) => Assert.True(value <= 10, $"value {value} is above ten");

    [GeneratedRegex("Set seed: \"(?<seed>[^\"]+)\"")]
    private static partial Regex SeedPattern();

    [GeneratedRegex(@"first failing input: [0-9]+")]
    private static partial Regex FirstInputPattern();

    [GeneratedRegex(@"value \d+ is above ten")]
    private static partial Regex CounterexamplePattern();

    [Fact]
    public void AFailingPropertyReportsTheSeedAndTheCounterexample()
    {
        var failure = Assert.Throws<XunitException>(
            () => PropertyRun.Check(Gen.Int[0, 1_000_000], AlwaysBelowEleven));

        Assert.Matches(SeedPattern(), failure.Message);
        Assert.Matches(CounterexamplePattern(), failure.Message);
        Assert.Contains(PropertyRun.SeedVariable, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportedSeedRepeatsTheSameFailureEveryTime()
    {
        var first = Assert.Throws<XunitException>(
            () => PropertyRun.Check(Gen.Int[0, 1_000_000], AlwaysBelowEleven));
        var seed = SeedPattern().Match(first.Message).Groups["seed"].Value;

        // The seed starts at the case that failed, so a repeat fails again at once and on the same input.
        var repeats = Enumerable.Range(0, 5)
            .Select(_ => Assert.Throws<XunitException>(
                () => PropertyRun.Check(Gen.Int[0, 1_000_000], AlwaysBelowEleven, seed)))
            .ToList();

        Assert.All(repeats, repeat => Assert.Matches(CounterexamplePattern(), repeat.Message));
        var inputs = repeats.Select(repeat => FirstInputPattern().Match(repeat.Message).Value).Distinct().ToList();
        _ = Assert.Single(inputs);
        Assert.NotEmpty(inputs[0]);
    }

    [Fact]
    public void ExceedingTheTimeBudgetIsAFailureAndNotAReducedRun()
    {
        var failure = Assert.Throws<XunitException>(
            () => PropertyRun.Check(Gen.Int, _ => Thread.Sleep(5), cases: 20, budget: TimeSpan.FromMilliseconds(10)));

        Assert.Contains("time limit exceeded", failure.Message, StringComparison.Ordinal);
        Assert.Contains("20 cases", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePropertyRunsTheDeclaredNumberOfCases()
    {
        var calls = 0;

        PropertyRun.Check(Gen.Int, _ => { calls++; }, cases: PropertyRun.Cases);

        Assert.Equal(PropertyRun.Cases, calls);
    }
}
