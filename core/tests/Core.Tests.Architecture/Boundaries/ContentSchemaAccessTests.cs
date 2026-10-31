using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>Every statement over the content schema is a constant of the list of queries, which the test with no domain declared runs, and none moves the search path.</summary>
public sealed class ContentSchemaAccessTests
{
    private const string Fixtures = "content-schema";
    private const string QueriesPath = "core/src/Core.Persistence/Content/ContentQueries.cs";

    [Fact]
    public void ProductionNamesNoContentTableOutsideTheListOfQueriesAndNeverMovesTheSearchPath()
    {
        var violations = ContentSchemaAccessRules.Check(SourceTree.ProductionFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionIncludesTheListOfQueriesTheRuleLeavesRoomFor()
    {
        var files = SourceTree.ProductionFiles();

        Assert.Contains(files, file => file.FileName == "ContentQueries.cs");
    }

    [Fact]
    public void PlainInterpolatedAndRawStatementsOverTheContentSchemaAreRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "ReachesContentDirectly.cs"));

        var lines = ContentSchemaAccessRules.Check([fixture]).Select(violation => violation.Line).Order();

        Assert.Equal([5, 7, 9], lines);
    }

    [Fact]
    public void SchemaNameMoldAndOrdinaryWordsAreNotAnAccess()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "NamesNoContentTable.cs"));

        var violations = ContentSchemaAccessRules.Check([fixture]);

        Assert.Empty(violations);
    }

    [Fact]
    public void ConstantOfTheListOfQueriesIsLeftAloneAndAStatementBuiltAtRunTimeThereIsNot()
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, "QueryList.cs"));
        var queries = SourceFile.FromText(QueriesPath, text);

        var lines = ContentSchemaAccessRules.Check([queries]).Select(violation => violation.Line);

        Assert.Equal([7], lines);
    }

    [Fact]
    public void QueryThatIsNotAPublicConstantIsRefusedBecauseTheListWouldNotSeeIt()
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, "HidesAQuery.cs"));
        var queries = SourceFile.FromText(QueriesPath, text);

        var lines = ContentSchemaAccessRules.Check([queries]).Select(violation => violation.Line).Order();

        Assert.Equal([7, 9, 11], lines);
    }

    [Fact]
    public void ConstantOfANestedOrOtherClassInTheFileOfTheListIsRefusedBecauseTheListWouldNotSeeIt()
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, "BesideTheList.cs"));
        var queries = SourceFile.FromText(QueriesPath, text);

        var lines = ContentSchemaAccessRules.Check([queries]).Select(violation => violation.Line).Order();

        Assert.Equal([9, 15], lines);
    }

    [Fact]
    public void StatementInAnotherFileOfTheContentFolderIsRefused()
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, "ReachesContentDirectly.cs"));
        var beside = SourceFile.FromText("core/src/Core.Persistence/Content/ContentReport.cs", text);

        var lines = ContentSchemaAccessRules.Check([beside]).Select(violation => violation.Line).Order();

        Assert.Equal([5, 7, 9], lines);
    }

    [Fact]
    public void EveryWayOfMovingTheSearchPathIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "MovesTheSearchPath.cs"));

        var lines = ContentSchemaAccessRules.Check([fixture]).Select(violation => violation.Line).Order();

        Assert.Equal([5, 7, 9, 11, 13, 15], lines);
    }

    [Fact]
    public void MovingTheSearchPathIsRefusedInsideTheListOfQueriesToo()
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, "MovesTheSearchPath.cs"));
        var queries = SourceFile.FromText(QueriesPath, text);

        var lines = ContentSchemaAccessRules.Check([queries]).Select(violation => violation.Line).Order();

        Assert.Equal([5, 7, 9, 11, 13, 15], lines);
    }

    [Fact]
    public void OnlyTheLocalPinOfTheSearchPathToTheCatalogIsLetThroughInTheServerGuarantees()
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, "PinsTheSearchPath.cs"));
        var probe = SourceFile.FromText("core/src/Core.Persistence/ServerGuarantees/ServerProbe.cs", text);

        var lines = ContentSchemaAccessRules.Check([probe]).Select(violation => violation.Line).Order();

        Assert.Equal([7, 9, 11, 13], lines);
    }

    [Fact]
    public void ThePinOfTheSearchPathToTheCatalogIsRefusedOutsideTheServerGuarantees()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "PinsTheSearchPath.cs"));

        var lines = ContentSchemaAccessRules.Check([fixture]).Select(violation => violation.Line).Order();

        Assert.Equal([5, 7, 9, 11, 13], lines);
    }
}
