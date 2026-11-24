using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>The code that writes a replacement over a text cuts it only through the boundary's converter, so that no offset reaches the replacement any other way.</summary>
public sealed class ReplacementCutTests
{
    [Fact]
    public void NoOffsetReachesTheReplacementWithoutTheConverter()
    {
        var files = SourceTree.ProductionFiles();

        var violations = SingleOffsetArithmeticRules.CutsInTheReplacement([.. files, .. GlobalUsings()]);

        Assert.Contains(files, file => SingleOffsetArithmeticRules.InTheReplacement(file.Path));
        Assert.Empty(violations);
    }

    [Fact]
    public void CutInTheReplacementIsRefusedWhateverOffsetItTakes()
    {
        var fixture = SourceTree.Fixture(Path.Combine("single-offset-arithmetic", "OffsetArithmeticFree.cs"));
        var inside = SourceFile.FromText(Path.Combine(SourceTree.CoreRoot, "src", "Core.Ingestion", "Pseudonymizing", fixture.FileName), fixture.Text);

        var lines = SingleOffsetArithmeticRules.CutsInTheReplacement([inside, .. ContractSources(), .. GlobalUsings()]).Select(violation => violation.Line).Distinct().Order();

        Assert.Equal([9, 11, 19], lines);
    }

    private static IEnumerable<SourceFile> ContractSources() =>
        SourceTree.ProductionFiles().Where(file => file.Path.Contains("Core.Boundary.Contracts", StringComparison.Ordinal));

    /// <summary>The usings the SDK adds to every production project, without which an extension method such as <c>Skip</c> would not bind.</summary>
    private static IEnumerable<SourceFile> GlobalUsings() =>
        SourceTree.ProductionProjects()
            .SelectMany(project => CompilerInvocation.Of(project.Path).BuildAddedFiles)
            .Where(file => file.FileName.EndsWith(".GlobalUsings.g.cs", StringComparison.Ordinal));
}
