using Core.Platform.Tests.Logging;

namespace Core.Platform.Tests.Analyzers;

/// <summary>A typical SonarAnalyzer finding in code under <c>core/</c> does not build, whether its default profile reports it or not.</summary>
public sealed class SonarRulesBuildTests
{
    private const string Fixture = "sonar-rules";
    private const string Project = "SonarRules.csproj";

    [Fact]
    public async Task UnusedPrivateMemberFailsTheBuildWithSonarRuleS1144()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var build = await DotnetBuild.OfFixtureAsync(Fixture, Project, cancellationToken);

        Assert.NotEqual(0, build.ExitCode);
        Assert.Contains(build.Lines, line => IsRuleIn("UnusedPrivateMember.cs", "S1144", line));
    }

    [Fact]
    public async Task BreakInALoopFailsTheBuildWithSonarRuleS1227ThatTheDefaultProfileLeavesOff()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var build = await DotnetBuild.OfFixtureAsync(Fixture, Project, cancellationToken);

        Assert.NotEqual(0, build.ExitCode);
        Assert.Contains(build.Lines, line => IsRuleIn("BreakOutsideSwitch.cs", "S1227", line));
    }

    [Fact]
    public async Task TheSonarRulesAreTheOnlyReasonTheFixtureFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var build = await DotnetBuild.OfFixtureAsync(Fixture, Project, cancellationToken);

        var errors = build.Lines.Where(line => line.Contains(": error ", StringComparison.Ordinal));
        Assert.All(errors, line => Assert.Matches(": error S(1144|1227):", line));
    }

    private static bool IsRuleIn(string fixtureFile, string rule, string line) =>
        line.Contains(fixtureFile, StringComparison.Ordinal) && line.Contains($"error {rule}", StringComparison.Ordinal);
}
