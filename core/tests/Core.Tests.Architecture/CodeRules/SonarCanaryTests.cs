namespace Core.Tests.Architecture.CodeRules;

/// <summary>A Sonar finding put in every type of every production file fails the build, so no file or type escapes the analyzer.</summary>
public sealed class SonarCanaryTests
{
    public static TheoryData<string> ProductionProjectNames() =>
        [.. SourceTree.ProductionProjects().Select(project => project.ModuleName)];

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public async Task EveryProductionFileReportsTheFindingPlantedInIt(string projectName)
    {
        var run = await CanaryRun.OfProjectAsync(projectName, TestContext.Current.CancellationToken);

        Assert.NotEmpty(run.SonarSites);
        Assert.Empty(LoggingCanaryRules.CheckBuildIsIntact(run.Build));
        Assert.Empty(SonarCanaryRules.CheckRefused(run.SonarSites, run.Build));
    }

    [Fact]
    public async Task SourcesThatTryToLookGeneratedStillHideNothingExceptTheOnesTheAnalyzerSkips()
    {
        var run = await CanaryRun.OfSonarFixtureAsync("Skips", TestContext.Current.CancellationToken);

        var hidden = SonarCanaryRules.CheckRefused(run.SonarSites, run.Build).Select(violation => Path.GetFileName(violation.File)).Distinct().Order().ToList();

        Assert.Empty(LoggingCanaryRules.CheckBuildIsIntact(run.Build));
        Assert.Equal(["Auto.cs", "DebugAttr.cs", "HeaderBy.cs", "Named.g.cs", "OwnAttr.cs", "Probe.designer.x.cs", "Probe.g.x.cs", "Probe_generated.cs", "TemporaryGeneratedFile_Probe.cs"], hidden);
    }
}
