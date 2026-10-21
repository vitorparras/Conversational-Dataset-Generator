using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>One place in the core counts code points and cuts the service's text at its offsets, with a fixture written every way the rule must still see.</summary>
public sealed class SingleOffsetArithmeticTests
{
    private const string Fixtures = "single-offset-arithmetic";

    [Fact]
    public void ProductionCodeCountsCodePointsAndCutsTheServiceTextOnlyInTheConverter()
    {
        var violations = SingleOffsetArithmeticRules.Check([.. SourceTree.ProductionFiles(), .. GlobalUsings()]);

        Assert.Empty(violations);
    }

    [Fact]
    public void ConverterFolderIsWhereProductionCodeCountsCodePoints()
    {
        var findings = SingleOffsetArithmeticRules.Find([.. SourceTree.ProductionFiles(), .. GlobalUsings()]).ToList();

        Assert.NotEmpty(findings);
        Assert.All(findings, finding => Assert.True(SingleOffsetArithmeticRules.InConverterFolder(finding.File), finding.ToString()));
    }

    [Fact]
    public void CountingOrCuttingOutsideTheConverterIsRefusedHoweverItIsWritten()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "OffsetArithmeticOutsideConverter.cs"));

        var lines = FindingsIn(fixture).Select(violation => violation.Line).Distinct().Order();

        Assert.Equal([4, 10, 13, 15, 18, 20, 22, 24, 26, 28, 30, 34, 36, 38, 41, 43, 45, 47, 49], lines);
    }

    [Fact]
    public void CodeThatNeitherCountsCodePointsNorCutsTheServiceTextIsAccepted()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "OffsetArithmeticFree.cs"));

        var violations = FindingsIn(fixture);

        Assert.Empty(violations);
    }

    [Fact]
    public void SameCodeInsideTheConverterFolderIsAccepted()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "OffsetArithmeticOutsideConverter.cs"));
        var inside = SourceFile.FromText(Path.Combine(SourceTree.CoreRoot, "src", "Core.Boundary", "Text", fixture.FileName), fixture.Text);

        var violations = SingleOffsetArithmeticRules.Check([inside, .. ContractSources(), .. GlobalUsings()]);

        Assert.Empty(violations);
    }

    private static List<Violation> FindingsIn(SourceFile fixture) =>
        [.. SingleOffsetArithmeticRules.Check([fixture, .. ContractSources(), .. GlobalUsings()]).Where(violation => violation.File == fixture.Path)];

    /// <summary>The contract the fixtures name, so that its offsets bind to the real members.</summary>
    private static IEnumerable<SourceFile> ContractSources() =>
        SourceTree.ProductionFiles().Where(file => file.Path.Contains("Core.Boundary.Contracts", StringComparison.Ordinal));

    /// <summary>The usings the SDK adds to every production project, without which an extension method such as <c>AsSpan</c> would not bind.</summary>
    private static IEnumerable<SourceFile> GlobalUsings() =>
        SourceTree.ProductionProjects()
            .SelectMany(project => CompilerInvocation.Of(project.Path).BuildAddedFiles)
            .Where(file => file.FileName.EndsWith(".GlobalUsings.g.cs", StringComparison.Ordinal));
}
