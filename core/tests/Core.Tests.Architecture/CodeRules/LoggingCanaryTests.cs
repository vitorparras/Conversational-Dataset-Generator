namespace Core.Tests.Architecture.CodeRules;

/// <summary>The banned logging routes fail the build of every production project, and each way of silencing them is caught by its effect.</summary>
public sealed class LoggingCanaryTests
{
    private const string SilencedProject = "Core.Platform";

    public static TheoryData<string> ProductionProjectNames() =>
        [.. SourceTree.ProductionProjects().Select(project => project.ModuleName)];

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public async Task EveryBannedRouteFailsTheBuildOfEveryFileOfAProductionProject(string projectName)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = new CanaryWorkspace(projectName);
        var sites = workspace.PlantCanaries();

        var build = await workspace.BuildAsync(cancellationToken);

        Assert.NotEmpty(sites);
        Assert.Empty(LoggingCanaryRules.CheckBuildIsIntact(build));
        Assert.Empty(LoggingCanaryRules.CheckRoutesRefused(sites, build));
    }

    [Theory]
    [InlineData("NoWarn")]
    [InlineData("RemovedBannedSymbolsByName")]
    [InlineData("RemovedAnalyzerPackage")]
    [InlineData("RemovedEveryAdditionalFile")]
    [InlineData("RemovedAnalyzerInTarget")]
    [InlineData("ExtraAnalyzerConfig")]
    public async Task SilencingTheAnalyzerLetsTheCanaryThroughAndIsRefused(string fixture)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = new CanaryWorkspace(SilencedProject);
        workspace.Apply(Path.Combine(SourceTree.FixturesDirectory, "logging-canary", fixture));
        var sites = workspace.PlantCanaries();

        var build = await workspace.BuildAsync(cancellationToken);

        Assert.Empty(LoggingCanaryRules.CheckBuildIsIntact(build));
        Assert.Equal(sites.Count * LoggingCanary.Routes.Count, LoggingCanaryRules.CheckRoutesRefused(sites, build).Count());
    }
}
