using Core.Ingestion.Contracts.Reversal;

namespace Core.Ingestion.Tests.Reversal;

/// <summary>
/// The request to reverse a marker (issue 103): it says which marker, in which item, why the analysis does not advance without the identity, what will be done with it and for how
/// long; a request without the four is not submitted, and one that reaches more than one marker or item, or no item, is refused by its scope.
/// </summary>
public sealed class ReversalRequestRuleTests
{
    private static readonly ReversalRequestDraft Complete = new()
    {
        Item = "118",
        Marker = "PESSOA_48291307",
        Term = "30",
        Reason = "zqx-por-que",
        Use = "zqx-uso",
    };

    public static TheoryData<ReversalRequestDraft, ReversalRequestField> Incomplete => new()
    {
        { Complete with { Reason = null }, ReversalRequestField.Reason },
        { Complete with { Reason = "  " }, ReversalRequestField.Reason },
        { Complete with { Use = "" }, ReversalRequestField.Use },
        { Complete with { Term = null }, ReversalRequestField.Term },
        { Complete with { Term = "0" }, ReversalRequestField.Term },
        { Complete with { Term = "trinta" }, ReversalRequestField.Term },
        { Complete with { Marker = " " }, ReversalRequestField.Marker },
        { Complete with { Marker = "PESSOA 48291307" }, ReversalRequestField.Marker },
        { Complete with { Item = "cento" }, ReversalRequestField.Item },
    };

    public static TheoryData<ReversalRequestDraft, ReversalScope> TooWide => new()
    {
        { Complete with { Item = "118, 204" }, ReversalScope.ManyItems },
        { Complete with { Marker = "PESSOA_48291307, LUGAR_00000001" }, ReversalScope.ManyMarkers },
        { Complete with { Item = "" }, ReversalScope.Domain },
        { Complete with { Item = null }, ReversalScope.Domain },
    };

    [Theory]
    [MemberData(nameof(Incomplete))]
    public void RequestWithoutReasonUseOrTermIsRefused(ReversalRequestDraft draft, ReversalRequestField missing)
    {
        var judged = ReversalRequestRule.Judge(draft);

        Assert.Equal((ReversalRequestVerdict.Incomplete, true, (ReversalRequest?)null), (judged.Verdict, judged.Missing.Contains(missing), judged.Request));
    }

    [Theory]
    [MemberData(nameof(TooWide))]
    public void RequestReachingMoreThanOneMarkerOrItemIsRefusedByScope(ReversalRequestDraft draft, ReversalScope scope)
    {
        var judged = ReversalRequestRule.Judge(draft);

        Assert.Equal((ReversalRequestVerdict.TooWide, (ReversalScope?)scope, (ReversalRequest?)null), (judged.Verdict, judged.Scope, judged.Request));
    }

    [Fact]
    public void RequestMissingEveryFieldNamesEachOneOfThem()
    {
        var judged = ReversalRequestRule.Judge(new ReversalRequestDraft { Item = "118" });

        Assert.Equal(new[] { ReversalRequestField.Marker, ReversalRequestField.Term, ReversalRequestField.Reason, ReversalRequestField.Use }, judged.Missing);
    }

    [Fact]
    public void IncompleteRequestIsNotJudgedByItsScope()
    {
        var judged = ReversalRequestRule.Judge(Complete with { Item = "118, 204", Use = null });

        Assert.Equal((ReversalRequestVerdict.Incomplete, (ReversalScope?)null), (judged.Verdict, judged.Scope));
    }

    [Fact]
    public void CompleteRequestOfOneMarkerInOneItemIsAccepted()
    {
        var judged = ReversalRequestRule.Judge(Complete with { Item = " 118 ", Marker = " LUGAR_00000001 ", Reason = " zqx-por-que " });

        Assert.Equal(
            (ReversalRequestVerdict.Accepted, new ReversalRequest { ItemId = 118, Marker = "LUGAR_00000001", TermDays = 30, Reason = "zqx-por-que", Use = "zqx-uso" }),
            (judged.Verdict, judged.Request));
    }
}
