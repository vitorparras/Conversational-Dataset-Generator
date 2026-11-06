using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>Only the rotation, the rewrap, the check of the epochs and the copy open the scope of the system, and only the content transaction names the setting that carries it.</summary>
public sealed class ContentScopeTests
{
    private const string Fixtures = "content-scope";
    private const string Folder = "core/src/Core.Persistence/";

    private static readonly string[] Beside =
    [
        "Content/Reading/ProtectedContentReader.cs", "Content/Writing/ContentWriter.cs", "ServerGuarantees/CopyTables.cs", "Ingestion/IngestionBatches.cs",
    ];

    [Fact]
    public void ProductionOpensTheScopeOfTheSystemOnlyWhereTheSystemOperationsAre()
    {
        var violations = ContentScopeRules.Check(SourceTree.ProductionFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionIncludesTheFilesTheRuleLeavesRoomFor()
    {
        var names = SourceTree.ProductionFiles().Select(file => file.FileName).ToList();

        Assert.All(["ContentTransaction.cs", "EpochRotation.cs", "DomainRewrap.cs", "ContentEpochCheck.cs", "DataCopy.cs"], name => Assert.Contains(name, names));
    }

    [Fact]
    public void ACallerOutsideTheSystemOperationsIsRefusedForEachMemberThatOpensTheScope()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "OpensTheScope.cs"));

        var lines = ContentScopeRules.Check([fixture]).Select(violation => violation.Line).Order();

        Assert.Equal([5, 7, 9], lines);
    }

    [Theory]
    [InlineData("Content/Rotation/EpochRotation.cs")]
    [InlineData("Content/Rotation/DomainRewrap.cs")]
    [InlineData("ServerGuarantees/ContentEpochCheck.cs")]
    [InlineData("ServerGuarantees/DataCopy.cs")]
    [InlineData("Content/ContentTransaction.cs")]
    public void TheFilesOfTheSystemOperationsMayOpenTheScope(string path)
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, "OpensTheScope.cs"));

        var violations = ContentScopeRules.Check([SourceFile.FromText(Folder + path, text)]);

        Assert.Empty(violations);
    }

    [Fact]
    public void OtherFilesOfTheSameFoldersAreRefused()
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, "OpensTheScope.cs"));

        var counts = Beside
            .Select(path => ContentScopeRules.Check([SourceFile.FromText(Folder + path, text)]).Count());

        Assert.All(counts, count => Assert.Equal(3, count));
    }

    [Fact]
    public void TheSettingIsNamedOnlyInTheContentTransactionPlainOrInterpolated()
    {
        var text = SourceTree.FixtureText(Path.Combine(Fixtures, "NamesTheSetting.cs"));

        var elsewhere = ContentScopeRules.Check([SourceFile.FromText(Folder + "Content/Rotation/EpochRotation.cs", text)]).Select(violation => violation.Line).Order();
        var owner = ContentScopeRules.Check([SourceFile.FromText(Folder + "Content/ContentTransaction.cs", text)]);

        Assert.Equal([5, 7], elsewhere);
        Assert.Empty(owner);
    }

    [Fact]
    public void TheDomainSettingAndTheOrdinaryTransactionAreNotTheScope()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "NamesNothingOfTheScope.cs"));

        var violations = ContentScopeRules.Check([fixture]);

        Assert.Empty(violations);
    }
}
