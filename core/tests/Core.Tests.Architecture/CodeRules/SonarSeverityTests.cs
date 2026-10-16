using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>The compiler gives every SonarAnalyzer rule as an error to every production source, whatever way the configs are written.</summary>
public sealed class SonarSeverityTests
{
    private const string Fixtures = "sonar-severity";
    private const string RootConfig = "severity.rules";

    private static readonly SonarRule[] SampleRules =
    [
        new("S1", DiagnosticSeverity.Warning, EnabledByDefault: false),
        new("S2", DiagnosticSeverity.Info, EnabledByDefault: true),
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

        var violations = SonarSeverityRules.Check(rules, invocation, File.ReadAllText(rootConfig));

        Assert.Contains(rules, rule => rule.NeedsExplicitSeverity);
        Assert.Contains(invocation.AnalyzerConfigs, path => Path.GetFullPath(path) == Path.GetFullPath(rootConfig));
        Assert.Empty(violations);
    }

    [Fact]
    public void ConfigTurningEveryOffRuleOnAndExplainingTheExclusionIsAccepted()
    {
        var violations = Judged("Complete");

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("MissingRule", "S2")]
    [InlineData("LoweredWithoutReason", "S3")]
    [InlineData("EmptyReason", "S2")]
    [InlineData("HeaderComment", "S2")]
    [InlineData("InlineComment", "S2")]
    [InlineData("Colon", "S2")]
    [InlineData("WrongSection", "S1")]
    [InlineData("GlobalConfig", "S3")]
    public void ConfigThatLeavesARuleBelowErrorWithoutAReasonIsRefused(string fixture, string rule)
    {
        var violations = Judged(fixture);

        var refused = Assert.Single(violations);
        Assert.StartsWith(rule + " ", refused.Message, StringComparison.Ordinal);
    }

    private static List<Violation> Judged(string fixture)
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine(Fixtures, fixture), "App");
        var rootConfig = SourceTree.FixtureText(Path.Combine(Fixtures, fixture, "App", RootConfig));

        return [.. SonarSeverityRules.Check(SampleRules, invocation, rootConfig)];
    }
}
