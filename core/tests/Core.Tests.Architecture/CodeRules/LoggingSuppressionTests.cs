namespace Core.Tests.Architecture.CodeRules;

/// <summary>The banned-symbol diagnostic cannot be silenced in production except on a class of generated log messages, with a fixture that breaks each part.</summary>
public sealed class LoggingSuppressionTests
{
    private const string Fixtures = "logging-suppression";
    private const string SourcesFixtures = "logging-suppression-sources";

    [Fact]
    public void ProductionSourcesSuppressTheBannedSymbolOnlyOnGeneratedLogMessages()
    {
        var violations = LoggingSuppressionRules.CheckSuppressions(SourceTree.ProductionFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionSourcesNeverDisableTheBannedSymbolByPragma()
    {
        var violations = SourceTree.ProductionFiles().SelectMany(LoggingSuppressionRules.CheckPragmas);

        Assert.Empty(violations);
    }

    [Fact]
    public void ClassOfGeneratedLogMessagesWithJustificationIsAccepted()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "ProperLogMessages.cs"));

        Assert.Empty(LoggingSuppressionRules.CheckSuppressions([fixture]));
    }

    [Fact]
    public void AssemblyLevelSuppressionIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "AssemblySuppression.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions([fixture]));

        Assert.Contains("assembly or module", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SuppressionOnAMemberIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "MemberSuppression.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions([fixture]));

        Assert.Equal(10, violation.Line);
    }

    [Fact]
    public void UnconditionalSuppressionIsRefusedLikeAnyOther()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "UnconditionalSuppression.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions([fixture]));

        Assert.Equal(8, violation.Line);
    }

    [Fact]
    public void SuppressionThroughAnAliasIsRefusedLikeAnyOther()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "AliasedSuppression.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions([fixture]));

        Assert.Equal(8, violation.Line);
    }

    [Fact]
    public void SuppressionThroughAGlobalAliasDeclaredInAnotherFileIsRefused()
    {
        var files = SourceTree.FixtureFiles(Path.Combine(Fixtures, "alias"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions(files));

        Assert.EndsWith("Reporter.cs", violation.File, StringComparison.Ordinal);
    }

    [Fact]
    public void SuppressionOnAClassThatIsNotLogMessagesIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "PlainClassSuppression.cs"));

        var violations = LoggingSuppressionRules.CheckSuppressions([fixture]).ToList();

        Assert.Contains(violations, violation => violation.Message.Contains("not a *LogMessages class", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Message.Contains("not static partial", StringComparison.Ordinal));
    }

    [Fact]
    public void SuppressionWithoutJustificationIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "WithoutJustification.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions([fixture]));

        Assert.Contains("without a Justification", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HandWrittenMethodInsideTheLogMessagesClassIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "HandWrittenMethod.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions([fixture]));

        Assert.Equal(14, violation.Line);
    }

    [Fact]
    public void HandWrittenMethodInAnotherDeclarationOfTheLogMessagesClassIsRefused()
    {
        var files = SourceTree.FixtureFiles(Path.Combine(Fixtures, "split"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions(files));

        Assert.EndsWith("SampleLogMessages.Extra.cs", violation.File, StringComparison.Ordinal);
        Assert.Equal(7, violation.Line);
    }

    [Fact]
    public void SuppressionWithANonLiteralIdIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "NonLiteralId.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions([fixture]));

        Assert.Contains("string literal", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SuppressionInAFileCompiledFromOutsideTheProjectDirectoryIsRefused()
    {
        var files = CompilerInvocation.OfFixture(Path.Combine(SourcesFixtures, "IncludedOutside"), "App").SourceFiles;

        var violations = LoggingSuppressionRules.CheckSuppressions(files).ToList();

        Assert.NotEmpty(violations);
        Assert.All(violations, violation => Assert.EndsWith("Suppressor.cs", violation.File, StringComparison.Ordinal));
    }

    [Fact]
    public void SuppressionInAFileTheProjectRemovesFromCompilationIsNotJudged()
    {
        var files = CompilerInvocation.OfFixture(Path.Combine(SourcesFixtures, "RemovedTemplates"), "App").SourceFiles;

        var violations = LoggingSuppressionRules.CheckSuppressions(files);

        Assert.DoesNotContain(files, file => file.FileName == "Suppressor.cs");
        Assert.Empty(violations);
    }

    [Fact]
    public void PragmaDisablingTheBannedSymbolIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "PragmaDisable.cs"));

        var violations = LoggingSuppressionRules.CheckPragmas(fixture).ToList();

        Assert.Equal([5, 12], violations.Select(violation => violation.Line));
    }
}
