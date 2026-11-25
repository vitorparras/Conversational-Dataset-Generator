using Core.Ingestion.Contracts.Reversal;

namespace Core.Ingestion.Tests.Reversal;

/// <summary>
/// The authorization of a request to reverse a marker (issue 104): only another person of the curation of the domain authorizes, never who asked, and until a moment that has not
/// passed yet.
/// </summary>
public sealed class ReversalAuthorizationRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly ReversalAuthorizationAsk ByAnotherCurator = new()
    {
        RequestedBy = 7,
        AuthorizedBy = 8,
        AuthorizerIsCuration = true,
        ValidUntil = Now.AddDays(30),
        Now = Now,
    };

    public static TheoryData<ReversalAuthorizationAsk, ReversalAuthorizationVerdict> Refused => new()
    {
        { ByAnotherCurator with { AuthorizedBy = 7 }, ReversalAuthorizationVerdict.OwnRequest },
        { ByAnotherCurator with { AuthorizerIsCuration = false }, ReversalAuthorizationVerdict.NotCuration },
        { ByAnotherCurator with { AuthorizedBy = 7, AuthorizerIsCuration = false }, ReversalAuthorizationVerdict.NotCuration },
        { ByAnotherCurator with { ValidUntil = Now }, ReversalAuthorizationVerdict.TermInThePast },
        { ByAnotherCurator with { ValidUntil = Now.AddTicks(-1) }, ReversalAuthorizationVerdict.TermInThePast },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void AuthorizationThatIsNotByAnotherCuratorUntilAMomentToComeIsRefused(ReversalAuthorizationAsk ask, ReversalAuthorizationVerdict verdict) =>
        Assert.Equal(verdict, ReversalAuthorizationRule.Judge(ask));

    [Fact]
    public void AnotherCuratorAuthorizesUntilAMomentToCome()
    {
        var verdict = ReversalAuthorizationRule.Judge(ByAnotherCurator with { ValidUntil = Now.AddTicks(1) });

        Assert.Equal(ReversalAuthorizationVerdict.Authorized, verdict);
    }

    [Fact]
    public void OwnRequestIsRefusedBeforeItsTermIsLookedAt()
    {
        var verdict = ReversalAuthorizationRule.Judge(ByAnotherCurator with { AuthorizedBy = 7, ValidUntil = Now.AddDays(-1) });

        Assert.Equal(ReversalAuthorizationVerdict.OwnRequest, verdict);
    }
}
