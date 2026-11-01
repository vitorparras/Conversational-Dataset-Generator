using Core.Persistence.Custody;
using Core.Persistence.ServerGuarantees;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The layers are set against each other by role, table and step: they agree when every command the server saw has its event, the server seeing
/// more or less than the chain is a difference that names both counts, and the one difference known today is named as known and not as new.
/// </summary>
public sealed class AuditComparisonTests
{
    private const string App = "app";
    private static readonly DateTimeOffset Noon = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EveryStepOfTheChainIsInTheMapOrDeclaredWithoutATableWithItsReason()
    {
        var accounted = AuditMap.Expectations.SelectMany(expectation => expectation.Steps).Concat(AuditMap.StepsWithoutTable.Keys).ToHashSet();

        var unaccounted = Enum.GetValues<CustodyStep>().Where(step => !accounted.Contains(step));

        Assert.Empty(unaccounted);
        Assert.All(AuditMap.StepsWithoutTable.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
    }

    [Fact]
    public void StepDeclaredWithoutATableIsNotAlsoInTheMap()
    {
        var mapped = AuditMap.Expectations.SelectMany(expectation => expectation.Steps).ToHashSet();

        Assert.DoesNotContain(AuditMap.StepsWithoutTable.Keys, mapped.Contains);
    }

    [Fact]
    public void LayersThatAgreeHaveNoDifference()
    {
        var seen = new[] { Entry(App, "public.person", "INSERT"), Entry(App, "public.person_change", "INSERT") };

        var differences = AuditComparison.Compare(seen, App, new Dictionary<CustodyStep, int> { [CustodyStep.PersonCreated] = 1 });

        Assert.Empty(differences);
    }

    [Fact]
    public void ServerSeeingAWriteTheChainDoesNotHoldIsADifferenceWithBothCounts()
    {
        var seen = new[] { Entry(App, "public.person", "UPDATE"), Entry(App, "public.person", "UPDATE"), Entry(App, "public.person_change", "INSERT") };

        var difference = Assert.Single(AuditComparison.Compare(seen, App, new Dictionary<CustodyStep, int> { [CustodyStep.PersonDeactivated] = 1 }));

        Assert.Equal((App, "public.person", 2, 1, null), (difference.Role, difference.Table, difference.Seen, difference.Recorded, difference.KnownGap));
    }

    [Fact]
    public void ChainHoldingMoreThanTheServerSawIsADifference()
    {
        var recorded = new Dictionary<CustodyStep, int> { [CustodyStep.RawTextRead] = 1, [CustodyStep.SanitizedTextRead] = 1 };

        var difference = Assert.Single(AuditComparison.Compare([Entry(App, "content.item_text", "SELECT")], App, recorded));

        Assert.Equal(("content.item_text", 1, 2), (difference.Table, difference.Seen, difference.Recorded));
    }

    [Fact]
    public void ReadsOfTheTwoContentTablesAreAccountedByTheirOwnSteps()
    {
        var seen = new[] { Entry(App, "content.item_text", "SELECT"), Entry(App, "content.reversal_map", "SELECT") };
        var recorded = new Dictionary<CustodyStep, int> { [CustodyStep.SanitizedTextRead] = 1, [CustodyStep.ReversalEntriesRead] = 1 };

        Assert.Empty(AuditComparison.Compare(seen, App, recorded));
    }

    [Fact]
    public void CredentialWrittenWithNoEventIsTheKnownDifferenceAndNamesTheIssue()
    {
        var difference = Assert.Single(AuditComparison.Compare([Entry(App, "public.person_credential", "UPDATE")], App, new Dictionary<CustodyStep, int>()));

        Assert.Equal(AuditMap.RecomputeWithoutEvent, difference.KnownGap);
        Assert.Contains("#71", difference.KnownGap, StringComparison.Ordinal);
    }

    [Fact]
    public void KnownGapDoesNotExplainTheChainHoldingMoreOrAnotherRole()
    {
        var recorded = new Dictionary<CustodyStep, int> { [CustodyStep.PersonPasswordSet] = 1 };

        var more = Assert.Single(AuditComparison.Compare([], App, recorded), difference => difference.Table == "public.person_credential");
        var other = Assert.Single(AuditComparison.Compare([Entry("owner", "public.person_credential", "UPDATE")], App, new Dictionary<CustodyStep, int>()));

        Assert.Null(more.KnownGap);
        Assert.Equal(("owner", null), (other.Role, other.KnownGap));
    }

    [Fact]
    public void WriteByAnotherRoleOnAMappedTableIsADifferenceOfThatRole()
    {
        var difference = Assert.Single(AuditComparison.Compare([Entry("owner", "public.person", "UPDATE")], App, new Dictionary<CustodyStep, int>()));

        Assert.Equal(("owner", 1, 0), (difference.Role, difference.Seen, difference.Recorded));
    }

    [Fact]
    public void CommandsOnTablesTheMapDoesNotHoldAreNotCompared()
    {
        var seen = new[] { Entry(App, "public.person", "SELECT"), Entry(App, "public.custody_event", "INSERT"), Entry(App, "content.item_text", "INSERT") };

        Assert.Empty(AuditComparison.Compare(seen, App, new Dictionary<CustodyStep, int>()));
    }

    private static ServerAuditEntry Entry(string role, string table, string command) =>
        new(Noon, role, "cdg", command == "SELECT" ? "READ" : "WRITE", command, "TABLE", table);
}
