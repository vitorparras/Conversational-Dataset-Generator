namespace Core.Tests.Architecture.CodeRules;

/// <summary>No analyzer is silenced in production, in code or on the compiler command line; the banned-symbol note of a generated log message class is judged elsewhere.</summary>
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
    public void ProductionProjectSilencesNoAnalyzerOnTheCommandLine(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);

        var violations = ProductionSuppressionRules.CheckOptions(invocation);

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

    [Fact]
    public void AnalyzerSilencedOnTheCommandLineIsRefused()
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine("sonar-switches", "Options"), "App");

        var messages = ProductionSuppressionRules.CheckOptions(invocation).Select(violation => violation.Message).ToList();

        Assert.Contains(messages, message => message.StartsWith("S1144 ", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.StartsWith("S1227 ", StringComparison.Ordinal));
    }
}
