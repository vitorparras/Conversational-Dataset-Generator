using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>The compiler gives every SonarAnalyzer rule as an error to every production source, whatever way the configs are written.</summary>
public sealed class SonarSeverityTests
{
    private const string Fixtures = "sonar-severity";
    private const string Switches = "sonar-switches";

    private static readonly Lazy<CompilerInvocation> AllLevelInvocation = new(() => CompilerInvocation.OfFixture(Path.Combine(Switches, "AnalysisLevelAll"), "App"));

    private static readonly RootConfig NoRoot = new(string.Empty, string.Empty, string.Empty);

    private static readonly SonarRule[] SampleRules =
    [
        new("S1", DiagnosticSeverity.Warning, EnabledByDefault: false),
        new("S1451", DiagnosticSeverity.Warning, EnabledByDefault: false),
        new("S3", DiagnosticSeverity.Warning, EnabledByDefault: true),
    ];

    public static TheoryData<string> ProductionProjectNames() =>
        [.. SourceTree.ProductionProjects().Select(project => project.ModuleName)];

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionProjectGetsEverySonarRuleOfTheRestoredAnalyzerAsAnError(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);
        var rules = SonarRuleCatalog.Load(SonarRuleCatalog.AnalyzerPath);

        var violations = SonarSeverityRules.Check(rules, invocation, RepositoryRoot());

        Assert.Contains(rules, rule => rule.NeedsExplicitSeverity);
        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionProjectLoadsTheSonarAnalyzerTheRulesWereReadFrom(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);

        var violations = SonarSeverityRules.CheckAnalyzerLoaded(invocation, SonarRuleCatalog.AnalyzerPath);

        Assert.Empty(violations);
        Assert.Empty(invocation.AnalyzerFailures);
    }

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionProjectIsGivenNoRuleSet(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);

        Assert.Empty(SonarSeverityRules.CheckNoRuleSet(invocation));
    }

    [Fact]
    public void ProjectThatRemovesTheSonarAnalyzerInATargetIsRefused()
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine(Switches, "AnalyzerRemoved"), "App");

        var violations = SonarSeverityRules.CheckAnalyzerLoaded(invocation, SonarRuleCatalog.AnalyzerPath);

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void ProjectWithARuleSetIsRefusedAndItsRuleReachesTheSourceSwitchedOff()
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine(Switches, "RuleSet"), "App");

        Assert.NotEmpty(SonarSeverityRules.CheckNoRuleSet(invocation));
        Assert.Equal(ReportDiagnostic.Suppress, invocation.SpecificDiagnosticOptions["S1227"]);
    }

    [Theory]
    [InlineData("Complete")]
    [InlineData("FileSpecific")]
    public void ConfigSwitchingARuleOffWithAReasonWhereItIsAllowedIsAccepted(string fixture)
    {
        var violations = Judged(fixture);

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("MissingRule", "S1451")]
    [InlineData("LoweredWithoutReason", "S3")]
    [InlineData("EmptyReason", "S1451")]
    [InlineData("HeaderComment", "S1451")]
    [InlineData("InlineComment", "S1451")]
    [InlineData("Colon", "S1451")]
    [InlineData("WrongSection", "S1")]
    [InlineData("GlobalConfig", "S3")]
    [InlineData("WideForOtherRule", "S1")]
    public void ConfigThatLeavesARuleBelowErrorWithoutAllowedReasonIsRefused(string fixture, string rule)
    {
        var violations = Judged(fixture);

        var refused = Assert.Single(violations);
        Assert.StartsWith(rule + " ", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionProjectIsGivenOnlyTheRootConfigAndTheOnesTheSdkBrings(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);

        Assert.NotEmpty(invocation.AnalyzerConfigPaths);
        Assert.Empty(AnalyzerConfigRules.CheckConfigFiles(invocation, RepositoryRoot()));
    }

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionProjectLowersNoDiagnosticInBulkOrByIdAndMarksNoSourceAsGenerated(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);

        Assert.Empty(AnalyzerConfigRules.CheckEffectiveSeverities(invocation, RepositoryRoot()));
    }

    [Theory]
    [InlineData("Complete")]
    [InlineData("FileSpecific")]
    [InlineData("SdkSwitchedOff")]
    public void ConfigsThatTheRootAndTheSdkMayWriteAreAcceptedByTheEffectiveSeverityRules(string fixture)
    {
        var (invocation, root) = Read(fixture);

        Assert.Empty(AnalyzerConfigRules.CheckEffectiveSeverities(invocation, root));
    }

    [Theory]
    [InlineData("BulkInFileSection", "dotnet_analyzer_diagnostic.severity")]
    [InlineData("BulkInAllSources", "dotnet_analyzer_diagnostic.severity")]
    [InlineData("BulkByCategory", "dotnet_analyzer_diagnostic.category-Style.severity")]
    [InlineData("GeneratedCodeMark", "generated_code")]
    [InlineData("LoweredNonSonarId", "CA1000")]
    [InlineData("DefaultSeverity", "CA1851")]
    [InlineData("GlobalDefault", "CA1851")]
    public void ConfigThatLowersDiagnosticsInBulkOrByIdOrMarksSourcesAsGeneratedIsRefused(string fixture, string expected)
    {
        var (invocation, root) = Read(fixture);

        var refused = AnalyzerConfigRules.CheckEffectiveSeverities(invocation, root).ToList();

        Assert.Contains(refused, violation => violation.Message.StartsWith(expected + " ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConfigBesideTheRootOneThatTheCompilerIsGivenIsRefused()
    {
        var (invocation, root) = Read("NestedConfig");

        var refused = AnalyzerConfigRules.CheckConfigFiles(invocation, root).ToList();

        var nested = Assert.Single(refused);
        Assert.EndsWith("nested.rules", nested.File, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectAtTheAllAnalysisLevelIsGivenOnlyConfigsOfTheSdk()
        => Assert.Empty(AnalyzerConfigRules.CheckConfigFiles(AllLevelInvocation.Value, NoRoot));

    [Fact]
    public void ProjectAtTheAllAnalysisLevelLoadsThePlatformAnalyzers()
        => Assert.Empty(SonarSeverityRules.CheckPlatformAnalyzersLoaded(AllLevelInvocation.Value));

    [Fact]
    public void ProjectAtAnAnalysisLevelBelowAllIsRefusedForTheConfigOfTheSdkItIsGiven()
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine(Switches, "AnalysisLevelMinimum"), "App");

        Assert.NotEmpty(AnalyzerConfigRules.CheckConfigFiles(invocation, NoRoot));
    }

    [Fact]
    public void ProjectWithTheNetAnalyzersSwitchedOffIsRefusedForTheAnalyzersItLacks()
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine(Switches, "NetAnalyzersOff"), "App");

        Assert.NotEmpty(SonarSeverityRules.CheckPlatformAnalyzersLoaded(invocation));
    }

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionProjectLoadsTheAnalyzersOfThePlatformFromTheSdk(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);

        Assert.Empty(SonarSeverityRules.CheckPlatformAnalyzersLoaded(invocation));
    }

    private static RootConfig RepositoryRoot()
    {
        var path = Path.Combine(Path.GetDirectoryName(SourceTree.CoreRoot)!, ".editorconfig");
        return new RootConfig(File.ReadAllText(path), Path.GetDirectoryName(path)!, path);
    }

    /// <summary>The compiler's own parser reads the command line a build would give for the fixture's config files and its one source.</summary>
    private static List<Violation> Judged(string fixture)
    {
        var (invocation, root) = Read(fixture);

        return [.. SonarSeverityRules.Check(SampleRules, invocation, root)];
    }

    private static (CompilerInvocation Invocation, RootConfig Root) Read(string fixture)
    {
        var directory = Path.Combine(SourceTree.FixturesDirectory, Fixtures, fixture, "App");
        var configs = Directory.EnumerateFiles(directory).Where(IsConfig);
        var arguments = configs.Select(path => "/analyzerconfig:" + path).Append("/nostdlib").Append(Path.Combine(directory, "Marker.cs")).ToList();
        var invocation = CompilerArguments.Read(new CompilerBuildOutput(Path.Combine(directory, "App.csproj"), arguments, Path.Combine(directory, "obj"), []));
        var rootPath = Path.Combine(directory, "severity.rules");

        return (invocation, new RootConfig(File.ReadAllText(rootPath), directory, rootPath));
    }

    private static bool IsConfig(string path) =>
        path.EndsWith(".rules", StringComparison.Ordinal) || path.EndsWith(".globalconfig", StringComparison.Ordinal);
}
