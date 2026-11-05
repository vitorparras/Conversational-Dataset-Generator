using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The event that records the verdict of a conference of the guarantees: written by the system, about the chain, with the mask of what is missing as the item id.</summary>
public sealed class GuaranteesVerifiedEventTests
{
    [Fact]
    public void EveryGuaranteeHeldIsAPassedVerificationByTheSystemOnTheChainWithAnEmptyMask()
    {
        var recorded = CustodyEvent.GuaranteesVerified(0);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 0), CustodyStep.VerifyGuarantees, CustodyReason.VerificationPassed, null, null),
            (recorded.Author, recorded.Item, recorded.Step, recorded.Reason, recorded.Reference, recorded.Corrects));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void AnyGuaranteeMissingIsAFailedVerificationCarryingTheMaskAsTheItemId(long missing)
    {
        var recorded = CustodyEvent.GuaranteesVerified(missing);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, missing), CustodyStep.VerifyGuarantees, CustodyReason.VerificationFailed, null, null),
            (recorded.Author, recorded.Item, recorded.Step, recorded.Reason, recorded.Reference, recorded.Corrects));
    }

    [Fact]
    public void ANegativeMaskIsRefusedByTheMissingParameter()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.GuaranteesVerified(-1));

        Assert.Equal("missing", refused.ParamName);
    }
}
