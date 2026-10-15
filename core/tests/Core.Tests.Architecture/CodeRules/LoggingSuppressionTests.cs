namespace Core.Tests.Architecture.CodeRules;

/// <summary>The banned-symbol diagnostic cannot be silenced in production except on a class of generated log messages, with a fixture that breaks each part.</summary>
public sealed class LoggingSuppressionTests
{
    private const string Fixtures = "logging-suppression";

    [Fact]
    public void ProductionSourcesSuppressTheBannedSymbolOnlyOnGeneratedLogMessages()
    {
        var violations = SourceTree.ProductionFiles().SelectMany(LoggingSuppressionRules.CheckSuppressions);

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionSourcesNeverDisableTheBannedSymbolByPragma()
    {
        var violations = SourceTree.ProductionFiles().SelectMany(LoggingSuppressionRules.CheckPragmas);

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionBuildFilesNeverSilenceTheBannedSymbol()
    {
        var violations = SourceTree.ProductionBuildFiles().SelectMany(LoggingSuppressionRules.CheckBuildFile);

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionAnalyzerConfigsKeepTheBannedSymbolAnError()
    {
        var configs = SourceTree.ProductionAnalyzerConfigs();

        var violations = configs.SelectMany(LoggingSuppressionRules.CheckAnalyzerConfig);

        Assert.NotEmpty(configs);
        Assert.Empty(violations);
    }

    [Fact]
    public void ClassOfGeneratedLogMessagesWithJustificationIsAccepted()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "ProperLogMessages.cs"));

        Assert.Empty(LoggingSuppressionRules.CheckSuppressions(fixture));
    }

    [Fact]
    public void AssemblyLevelSuppressionIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "AssemblySuppression.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions(fixture));

        Assert.Contains("assembly or module", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SuppressionOnAMemberIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "MemberSuppression.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions(fixture));

        Assert.Equal(10, violation.Line);
    }

    [Fact]
    public void SuppressionOnAClassThatIsNotLogMessagesIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "PlainClassSuppression.cs"));

        var violations = LoggingSuppressionRules.CheckSuppressions(fixture).ToList();

        Assert.Contains(violations, violation => violation.Message.Contains("not a *LogMessages class", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Message.Contains("not static partial", StringComparison.Ordinal));
    }

    [Fact]
    public void SuppressionWithoutJustificationIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "WithoutJustification.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions(fixture));

        Assert.Contains("without a Justification", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HandWrittenMethodInsideTheLogMessagesClassIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "HandWrittenMethod.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions(fixture));

        Assert.Equal(14, violation.Line);
    }

    [Fact]
    public void SuppressionWithANonLiteralIdIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "NonLiteralId.cs"));

        var violation = Assert.Single(LoggingSuppressionRules.CheckSuppressions(fixture));

        Assert.Contains("string literal", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PragmaDisablingTheBannedSymbolIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "PragmaDisable.cs"));

        var violations = LoggingSuppressionRules.CheckPragmas(fixture).ToList();

        Assert.Equal([5, 12], violations.Select(violation => violation.Line));
    }

    [Fact]
    public void NoWarnSilencingTheBannedSymbolIsRefused()
    {
        var project = SourceTree.FixtureProject(Path.Combine(Fixtures, "NoWarnBanned.csproj.txt"));

        var violations = LoggingSuppressionRules.CheckBuildFile(project).ToList();

        Assert.Equal([4, 5], violations.Select(violation => violation.Line));
    }

    [Fact]
    public void AnalyzerConfigLoweringTheBannedSymbolIsRefused()
    {
        var config = new AnalyzerConfig("downgrade.editorconfig", SourceTree.FixtureText(Path.Combine(Fixtures, "downgrade.editorconfig.txt")));

        var violations = LoggingSuppressionRules.CheckAnalyzerConfig(config).ToList();

        Assert.Equal([5, 8], violations.Select(violation => violation.Line));
    }
}
