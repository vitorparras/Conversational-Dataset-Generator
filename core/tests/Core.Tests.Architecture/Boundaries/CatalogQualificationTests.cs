using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>Every statement of the server guarantees names the catalog with pg_catalog., so a schema ahead of it in the search path cannot shadow what the check reads.</summary>
public sealed class CatalogQualificationTests
{
    private const string Fixtures = "catalog-qualification";
    private const string Folder = "core/src/Core.Persistence/ServerGuarantees/";

    [Fact]
    public void ProductionStatementsOfTheServerGuaranteesNameEveryCatalogObjectWithPgCatalog()
    {
        var violations = CatalogQualificationRules.Check(SourceTree.ProductionFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionIncludesTheChecksTheRuleReads()
    {
        var files = SourceTree.ProductionFiles();

        Assert.Contains(files, file => file.FileName == "RlsForceCheck.cs");
    }

    [Fact]
    public void BareTablesViewsFunctionsAggregatesAndQuotedAndInterpolatedAndRawStatementsAreRefused()
    {
        var check = Check("NamesTheCatalogBare.cs", Folder + "NamesTheCatalogBare.cs");

        Assert.Equal([5, 7, 9, 11, 13, 15, 17, 19, 21], check.Select(violation => violation.Line).Order());
    }

    [Fact]
    public void ViolationNamesWhatIsBare()
    {
        var violation = Check("NamesTheCatalogBare.cs", Folder + "NamesTheCatalogBare.cs").First(found => found.Line == 9);

        Assert.Contains("pg_has_role", violation.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pg_roles", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QualifiedNamesSyntaxLiteralsAndMessagesAreNotRefused()
    {
        var violations = Check("QualifiesTheCatalog.cs", Folder + "QualifiesTheCatalog.cs");

        Assert.Empty(violations);
    }

    [Fact]
    public void StatementOutsideTheServerGuaranteesIsLeftAlone()
    {
        var violations = Check("NamesTheCatalogBare.cs", "core/src/Core.Persistence/Other/NamesTheCatalogBare.cs");

        Assert.Empty(violations);
    }

    private static IReadOnlyList<Violation> Check(string fixture, string path)
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, fixture));

        return [.. CatalogQualificationRules.Check([SourceFile.FromText(path, text)])];
    }
}
