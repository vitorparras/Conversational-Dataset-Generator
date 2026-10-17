namespace Core.Tests.Architecture.CodeRules;

/// <summary>The banned logging routes fail the build of every production project and of a project that does not reference logging.</summary>
public sealed class LoggingCanaryTests
{
    public static TheoryData<string> ProductionProjectNames() =>
        [.. SourceTree.ProductionProjects().Select(project => project.ModuleName)];

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public async Task EveryBannedRouteFailsTheBuildOfEveryFileOfAProductionProject(string projectName)
    {
        var run = await CanaryRun.OfProjectAsync(projectName, TestContext.Current.CancellationToken);

        Assert.NotEmpty(run.Sites);
        Assert.Empty(LoggingCanaryRules.CheckBuildIsIntact(run.Build));
        Assert.Empty(LoggingCanaryRules.CheckRoutesRefused(run.Sites, run.Build));
    }

    [Fact]
    public async Task ProjectThatDoesNotReferenceLoggingStillRefusesEveryBannedRoute()
    {
        var run = await CanaryRun.OfFixtureProjectAsync("Core.Plain", TestContext.Current.CancellationToken);

        Assert.NotEmpty(run.Sites);
        Assert.Empty(LoggingCanaryRules.CheckBuildIsIntact(run.Build));
        Assert.Empty(LoggingCanaryRules.CheckRoutesRefused(run.Sites, run.Build));
    }
}
