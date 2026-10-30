using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>Every statement over the content schema goes through the content transaction, which declares the domain; the fixtures reach it from outside.</summary>
public sealed class ContentSchemaAccessTests
{
    private const string Fixtures = "content-schema";

    [Fact]
    public void ProductionNamesNoContentTableOutsideTheContentTransaction()
    {
        var violations = ContentSchemaAccessRules.Check(SourceTree.ProductionFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionIncludesTheContentTransactionTheRuleLeavesOut()
    {
        var files = SourceTree.ProductionFiles();

        Assert.Contains(files, file => file.FileName == "ContentTransaction.cs");
    }

    [Fact]
    public void PlainInterpolatedAndRawStatementsOverTheContentSchemaAreRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "ReachesContentDirectly.cs"));

        var lines = ContentSchemaAccessRules.Check([fixture]).Select(violation => violation.Line).Order();

        Assert.Equal([5, 7, 10], lines);
    }

    [Fact]
    public void SchemaNameMoldAndOrdinaryWordsAreNotAnAccess()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "NamesNoContentTable.cs"));

        var violations = ContentSchemaAccessRules.Check([fixture]);

        Assert.Empty(violations);
    }

    [Fact]
    public void StatementInsideTheContentTransactionFolderIsLeftAlone()
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, "ReachesContentDirectly.cs"));
        var inside = SourceFile.FromText("core/src/Core.Persistence/Content/Reaches.cs", text);

        var violations = ContentSchemaAccessRules.Check([inside]);

        Assert.Empty(violations);
    }
}
