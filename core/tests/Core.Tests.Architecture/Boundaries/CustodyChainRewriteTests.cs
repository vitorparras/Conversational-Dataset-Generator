using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>No code changes the events of the chain of custody: no statement updates, deletes, truncates, merges or upserts over the table (requirement 020; the database refuses the same).</summary>
public sealed class CustodyChainRewriteTests
{
    private const string Fixtures = "custody-chain";

    [Fact]
    public void ProductionOnlyAddsToAndReadsTheEventsOfTheChain()
    {
        var violations = CustodyChainRewriteRules.Check(SourceTree.ProductionFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionIncludesTheCodeThatWritesTheChainTheRuleWatches()
    {
        var files = SourceTree.ProductionFiles();

        Assert.Contains(files, file => file.FileName == "CustodyStatements.cs");
    }

    [Fact]
    public void UpdateDeleteTruncateMergeAndUpsertOverTheTableAreRefusedWhateverTheWayTheNameIsWritten()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "RewritesTheChain.cs"));

        var lines = CustodyChainRewriteRules.Check([fixture]).Select(violation => violation.Line).Order();

        Assert.Equal([5, 7, 9, 13, 15, 17], lines);
    }

    [Fact]
    public void StatementJoinedOrInterpolatedFromConstantsIsReadAsTheWholeTextItIs()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "RewritesThroughConstants.cs"));

        var lines = CustodyChainRewriteRules.Check([fixture]).Select(violation => violation.Line).Order();

        Assert.Equal([8, 10, 12, 14, 16, 19, 21], lines);
    }

    [Fact]
    public void AppendReadTriggerRepairAnotherTableASentenceAndAReadJoinedFromConstantsAreNotARewrite()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "OnlyReadsAndAppends.cs"));

        var violations = CustodyChainRewriteRules.Check([fixture]);

        Assert.Empty(violations);
    }
}
