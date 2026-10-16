namespace Core.Tests.Architecture.CodeRules;

/// <summary>What the compiler is given for each production project keeps the banned-symbol diagnostic an error, with a fixture for each way of lowering it.</summary>
public sealed class LoggingInvocationTests
{
    private const string Fixtures = "logging-invocation";

    public static TheoryData<string> ProductionProjectNames() =>
        [.. SourceTree.ProductionProjects().Select(project => project.ModuleName)];

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionProjectKeepsTheBannedSymbolAnErrorOnEverySource(string projectName)
    {
        var invocation = CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path);

        var violations = LoggingInvocationRules.Check(invocation);

        Assert.NotEmpty(invocation.Sources);
        Assert.Empty(violations);
    }

    [Fact]
    public void ProjectWithNoConfigOfItsOwnKeepsTheSeverity()
    {
        var invocation = Invocation("Untouched");

        var violations = LoggingInvocationRules.CheckSeverityOnEverySource(invocation);

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("NamedSectionLowered")]
    [InlineData("ColonSeparator")]
    [InlineData("LowercaseId")]
    [InlineData("TrailingComment")]
    [InlineData("GlobalConfigFirstLine")]
    public void ConfigLoweringTheSeverityIsRefusedHoweverItIsWritten(string fixture)
    {
        var invocation = Invocation(fixture);

        var violations = LoggingInvocationRules.CheckSeverityOnEverySource(invocation).ToList();

        Assert.NotEmpty(violations);
        Assert.All(violations, violation => Assert.Contains("only error is allowed", violation.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void NoWarnThatHidesFromTheCanaryIsStillSeenOnTheCommandLine()
    {
        var invocation = Invocation("ConditionalNoWarn");

        var violations = LoggingInvocationRules.CheckNotIgnored(invocation);

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void SourceAddedByATargetIsJudgedLikeAnyOther()
    {
        var files = Invocation("SourceAddedByTarget").SourceFiles;

        var violations = LoggingSuppressionRules.CheckSuppressions(files).ToList();

        Assert.NotEmpty(violations);
        Assert.All(violations, violation => Assert.EndsWith("Reporter.cs", violation.File, StringComparison.Ordinal));
    }

    private static CompilerInvocation Invocation(string fixture) =>
        CompilerInvocation.OfFixture(Path.Combine(Fixtures, fixture), "App");
}
