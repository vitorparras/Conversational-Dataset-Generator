namespace Core.Tests.Architecture.CodeRules;

/// <summary>No analyzer is silenced in production, in code, in what the build generates or on the compiler command line; the banned-symbol note of a log message class is judged elsewhere.</summary>
public sealed class ProductionSuppressionTests
{
    private const string Fixtures = "production-suppression";
    private const string Switches = "sonar-switches";

    private static readonly Lazy<CompilerInvocation> OptionsInvocation = new(() => CompilerInvocation.OfFixture(Path.Combine(Switches, "Options"), "App"));

    public static TheoryData<string> ProductionProjectNames() =>
        [.. SourceTree.ProductionProjects().Select(project => project.ModuleName)];

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionProjectSilencesNothingByPragmaOrAttributeInItsSourcesOrGeneratedFiles(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);

        var violations = ProductionSuppressionRules.CheckSources([.. invocation.SourceFiles, .. invocation.GeneratedFiles]);

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

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionProjectProducesOnlyKnownGeneratedFilesAndCarriesOnlySdkSuppressors(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);

        Assert.Empty(BuildOutputRules.CheckGeneratedFiles(invocation));
        Assert.Empty(BuildOutputRules.CheckSuppressors(invocation));
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
    public void AnalyzerSilencedByNoWarnOnTheCommandLineIsRefused()
    {
        var messages = OptionViolations();

        Assert.Contains(messages, message => message.StartsWith("S1144 ", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzerKeptFromFailingTheBuildOnTheCommandLineIsRefused()
    {
        var messages = OptionViolations();

        Assert.Contains(messages, message => message.StartsWith("S1227 ", StringComparison.Ordinal));
    }

    [Fact]
    public void SuppressorOfASonarRuleFromAPackageIsRefused()
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine(Switches, "Suppressor"), "App");

        var violations = BuildOutputRules.CheckSuppressors(invocation).ToList();

        Assert.Contains(violations, violation => violation.Message.Contains("S3358", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceCopiedIntoTheBuildOutputWithAPragmaIsRefusedByNameAndByContent()
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine(Switches, "ObjCopy"), "App");

        Assert.NotEmpty(BuildOutputRules.CheckGeneratedFiles(invocation));
        Assert.NotEmpty(ProductionSuppressionRules.CheckSources(invocation.GeneratedFiles));
    }

    [Theory]
    [InlineData("ObjGlobalUsings", "Sneak.GlobalUsings.g.cs")]
    [InlineData("ObjSubfolder", "Logging.g.cs")]
    public void SourceCopiedIntoTheBuildOutputUnderAGeneratedNameIsRefused(string fixture, string fileName)
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine(Switches, fixture), "App");

        var refused = BuildOutputRules.CheckGeneratedFiles(invocation).Select(violation => Path.GetFileName(violation.File)).ToList();

        Assert.Contains(fileName, refused);
    }

    [Fact]
    public void GeneratorThatEmitsASuppressionOfASonarRuleIsRefusedByNameAndByContent()
    {
        var invocation = CompilerInvocation.OfFixture(Path.Combine(Switches, "GenSuppress"), "App");

        Assert.NotEmpty(BuildOutputRules.CheckGeneratedFiles(invocation));
        Assert.NotEmpty(ProductionSuppressionRules.CheckSources(invocation.GeneratedFiles));
    }

    private static List<string> OptionViolations() =>
        [.. ProductionSuppressionRules.CheckOptions(OptionsInvocation.Value).Select(violation => violation.Message)];
}
