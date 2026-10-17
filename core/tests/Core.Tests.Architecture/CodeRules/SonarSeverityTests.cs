using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>The compiler gives every SonarAnalyzer rule as an error to every production source, whatever way the configs are written.</summary>
public sealed class SonarSeverityTests
{
    private const string Fixtures = "sonar-severity";
    private const string Switches = "sonar-switches";

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
        var rootConfig = Path.Combine(Path.GetDirectoryName(SourceTree.CoreRoot)!, ".editorconfig");

        var violations = SonarSeverityRules.Check(rules, invocation, new RootConfig(File.ReadAllText(rootConfig), Path.GetDirectoryName(rootConfig)!));

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

    /// <summary>The compiler's own parser reads the command line a build would give for the fixture's config files and its one source.</summary>
    private static List<Violation> Judged(string fixture)
    {
        var directory = Path.Combine(SourceTree.FixturesDirectory, Fixtures, fixture, "App");
        var configs = Directory.EnumerateFiles(directory).Where(IsConfig);
        var arguments = configs.Select(path => "/analyzerconfig:" + path).Append("/nostdlib").Append(Path.Combine(directory, "Marker.cs")).ToList();
        var invocation = CompilerArguments.Read(new CompilerBuildOutput(Path.Combine(directory, "App.csproj"), arguments, Path.Combine(directory, "obj"), []));
        var root = new RootConfig(File.ReadAllText(Path.Combine(directory, "severity.rules")), directory);

        return [.. SonarSeverityRules.Check(SampleRules, invocation, root)];
    }

    private static bool IsConfig(string path) =>
        path.EndsWith(".rules", StringComparison.Ordinal) || path.EndsWith(".globalconfig", StringComparison.Ordinal);
}
