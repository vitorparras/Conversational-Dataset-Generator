namespace Core.Tests.Architecture.CodeRules;

/// <summary>No Sonar rule is silenced in production, in code or on the compiler command line; the banned-symbol note of a generated log message class is judged elsewhere.</summary>
public sealed class ProductionSuppressionTests
{
    private const string Fixtures = "production-suppression";

    public static TheoryData<string> ProductionProjectNames() =>
        [.. SourceTree.ProductionProjects().Select(project => project.ModuleName)];

    [Fact]
    public void ProductionSourcesSilenceNothingByPragmaOrAttributeBesidesTheGeneratedLogMessages()
    {
        var violations = ProductionSuppressionRules.CheckSources(SourceTree.ProductionFiles());

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionProjectSilencesNoSonarRuleOnTheCommandLine(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);

        var violations = ProductionSuppressionRules.CheckSwitches(invocation);

        Assert.Empty(violations);
    }

    [Fact]
    public void ClassOfGeneratedLogMessagesWithTheBannedSymbolNoteIsAccepted()
    {
        var fixture = SourceTree.Fixture(Path.Combine("logging-suppression", "ProperLogMessages.cs"));

        Assert.Empty(ProductionSuppressionRules.CheckSources([fixture]));
    }

    [Theory]
    [InlineData("PragmaDisableS1227.cs")]
    [InlineData("SuppressMessageOnS1144.cs")]
    public void SuppressionInCodeIsRefused(string fixtureFile)
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, fixtureFile));

        var violations = ProductionSuppressionRules.CheckSources([fixture]);

        Assert.NotEmpty(violations);
    }

    [Theory]
    [InlineData("NoWarnSonar", "/nowarn")]
    [InlineData("WarningsNotAsErrorsSonar", "/warnaserror-")]
    public void SonarRuleSilencedOnTheCommandLineIsRefused(string fixture, string compilerSwitch)
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine("sonar-severity", fixture), "App");

        var violations = ProductionSuppressionRules.CheckSwitches(invocation).ToList();

        var refused = Assert.Single(violations);
        Assert.Contains(compilerSwitch, refused.Message, StringComparison.Ordinal);
    }
}
