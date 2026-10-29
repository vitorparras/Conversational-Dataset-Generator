namespace Core.Tests.Architecture.CodeRules;

/// <summary>Every test that starts the real language service says so with its category, with fixtures that break each way of reaching it.</summary>
public sealed class RealServiceMarkTests
{
    /// <summary>Marked by the change of issue #407, which owns the file; the rule leaves it out until then.</summary>
    private static readonly HashSet<string> Pending = ["RecognizerSelectionTests"];

    private static readonly HashSet<string> None = [];

    [Fact]
    public void EveryTestThatStartsTheRealServiceCarriesItsCategory()
    {
        var violations = RealServiceMarkRules.Check(SourceTree.TestFiles(), Pending);

        Assert.Empty(violations);
    }

    [Fact]
    public void TestsThatReachTheRealServiceByAnyRouteWithoutTheMarkAreNamed()
    {
        var violations = RealServiceMarkRules.Check(SourceTree.FixtureFiles("real-service-marks"), None).ToList();

        Assert.Equal(
            [
                "SetUpWithAStandInTests.FindsTheStandInStartedBeforeEveryTest",
                "CreatesATypeThatHoldsTheInterpreterTests.CreatesIt",
                "UnmarkedTests.StartsTheRealHostDirectly",
                "UnmarkedTests.InstallsTheRealServiceInTheHost",
                "UnmarkedTests.AsksTheLoggedHostForTheRealService",
                "UnmarkedTests.ReachesTheRealServiceThroughAHelper",
                "UnmarkedTests.NeedsTheInterpreterUvResolves",
                "UnmarkedTests.ReachesTheRealServiceThroughTwoHelpers",
                "WrongMarkTests.StartsTheRealHost",
            ],
            violations.Select(violation => violation.Message.Split(' ')[0]));
    }

    [Fact]
    public void ViolationNamesTheFileTheLineAndTheMarkThatIsMissing()
    {
        var violation = RealServiceMarkRules.Check(SourceTree.FixtureFiles("real-service-marks"), None)
            .First(candidate => candidate.Message.StartsWith("UnmarkedTests.StartsTheRealHostDirectly", StringComparison.Ordinal));

        Assert.EndsWith("Unmarked.cs", violation.File, StringComparison.Ordinal);
        Assert.Equal(5, violation.Line);
        Assert.Contains("[Trait(\"Category\", \"RealService\")]", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkOnTheMethodOrOnTheClassSatisfiesTheRule()
    {
        var marked = SourceTree.FixtureFiles("real-service-marks").Where(file => file.FileName.StartsWith("MarkedOn", StringComparison.Ordinal));

        var violations = RealServiceMarkRules.Check([.. marked], None);

        Assert.Empty(violations);
    }

    [Fact]
    public void TypeLeftPendingIsNotReproved()
    {
        var everyType = RealServiceMarkRules.Check(SourceTree.FixtureFiles("real-service-marks"), None).Select(violation => violation.Message.Split('.')[0]);
        var violations = RealServiceMarkRules.Check(SourceTree.FixtureFiles("real-service-marks"), everyType.ToHashSet(StringComparer.Ordinal));

        Assert.Empty(violations);
    }

    [Fact]
    public void NameTwoMembersShareIsNotTakenForAHelperThatReachesTheService()
    {
        var helpers = RealServiceUse.HelperNames(SourceTree.FixtureFiles("real-service-marks"));

        Assert.Contains("HelperHoldingTheRealService", helpers);
        Assert.Contains("OuterHelper", helpers);
        Assert.DoesNotContain("StartAsync", helpers);
    }
}
