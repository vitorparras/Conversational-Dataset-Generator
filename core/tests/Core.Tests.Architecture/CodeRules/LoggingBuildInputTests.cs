namespace Core.Tests.Architecture.CodeRules;

/// <summary>What a build is given besides its configs, whether analyzers, generators, additional files or
/// sources, is judged by what it does, with a fixture for each way of slipping something in.</summary>
public sealed class LoggingBuildInputTests
{
    private const string Fixtures = "logging-invocation";

    [Fact]
    public void BuildThatRunsNoAnalyzerIsRefused()
    {
        var invocation = Invocation("RunAnalyzersOff");

        var violations = LoggingInvocationRules.CheckDiagnosticOptions(invocation);

        Assert.Contains(violations, violation => violation.Message.Contains("no analyzer", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzerFromAPathOfItsOwnIsRefused()
    {
        var invocation = Invocation("OwnAnalyzer");

        var violations = LoggingInvocationRules.CheckAnalyzers(invocation);

        Assert.Contains(violations, violation => violation.Message.Contains("Own.Analyzer.dll", StringComparison.Ordinal));
    }

    [Fact]
    public void RazorFileHandedToTheCompilerAsAnAdditionalFileIsRefused()
    {
        var invocation = Invocation("RazorAdditionalFile");

        var violations = LoggingInvocationRules.CheckAdditionalFiles(invocation);

        Assert.Contains(violations, violation => violation.Message.Contains("Page.razor", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceAddedByAResponseFileIsJudgedLikeAnyOther()
    {
        var files = Invocation("RspAddsSource").SourceFiles;

        var violations = LoggingSuppressionRules.CheckSuppressions(files).ToList();

        Assert.NotEmpty(violations);
        Assert.All(violations, violation => Assert.EndsWith("Reporter.cs", violation.File, StringComparison.Ordinal));
    }

    [Fact]
    public void SourceAddedByATargetIsJudgedLikeAnyOther()
    {
        var files = Invocation("SourceAddedByTarget").SourceFiles;

        var violations = LoggingSuppressionRules.CheckSuppressions(files).ToList();

        Assert.NotEmpty(violations);
        Assert.All(violations, violation => Assert.EndsWith("Reporter.cs", violation.File, StringComparison.Ordinal));
    }

    [Fact]
    public void SuppressorIsRefusedEvenWhenABuildTargetMovesTheTargetingPackRoot()
    {
        var invocation = Invocation("RootMoved");

        var violations = LoggingInvocationRules.CheckAnalyzers(invocation).ToList();

        Assert.Contains(violations, violation => violation.Message.Contains("able to suppress", StringComparison.Ordinal));
    }

    [Fact]
    public void SuppressorFromAPackageInTheTrustedCacheIsRefusedForWhatItDoes()
    {
        var invocation = Invocation("LocalFeed");

        var violations = LoggingInvocationRules.CheckAnalyzers(invocation).ToList();

        Assert.Contains(violations, violation => violation.Message.Contains("able to suppress", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, violation => violation.Message.Contains("outside the SDK", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratorThatEmitsAnAssemblyLevelSuppressionIsRefused()
    {
        var invocation = Invocation("GenAssembly");

        var violations = LoggingSuppressionRules.Check(invocation.SourceFiles, invocation.GeneratedFiles).ToList();

        Assert.Contains(violations, violation => violation.Message.Contains("generated code suppresses", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceCopiedIntoTheBuildOutputIsJudgedLikeGeneratedCode()
    {
        var invocation = Invocation("SourceCopiedToObj");

        var violations = LoggingSuppressionRules.Check(invocation.SourceFiles, invocation.GeneratedFiles).ToList();

        Assert.Contains(violations, violation => violation.File.EndsWith("Reporter.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void AliasDeclaredInTheProjectFileReachesTheSourcesAndIsRefused()
    {
        var invocation = Invocation("AliasProj");

        var violations = LoggingSuppressionRules.Check(invocation.SourceFiles, invocation.GeneratedFiles).ToList();

        Assert.Contains(violations, violation => violation.File.EndsWith("Caller.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void AliasFromACommonSourceBindsTheAttributeInACopiedFile()
    {
        var invocation = Invocation("AliasCopy");

        var violations = LoggingSuppressionRules.Check(invocation.SourceFiles, invocation.GeneratedFiles).ToList();

        Assert.Contains(violations, violation => violation.File.EndsWith("Hidden.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratorOutputIsJudgedEvenWhenTheBuildScrubsItsOwnGeneratedFolder()
    {
        var invocation = Invocation("GenScrub");

        var violations = LoggingSuppressionRules.Check(invocation.SourceFiles, invocation.GeneratedFiles).ToList();

        Assert.Contains(violations, violation => violation.Message.Contains("generated code suppresses", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzerThatCannotBeLoadedFailsTheRulesClosed()
    {
        var invocation = Invocation("OwnAnalyzer");

        var violations = LoggingInvocationRules.CheckAnalyzers(invocation);

        Assert.Contains(violations, violation => violation.Message.Contains("could not be loaded", StringComparison.Ordinal));
    }

    private static CompilerInvocation Invocation(string fixture) =>
        CompilerInvocation.OfFixture(Path.Combine(Fixtures, fixture), "App");
}
