using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The events of the window of the owner credential (#64): written by the system, about the chain, with the version reached or the mask of the guarantees missing as the item id.</summary>
public sealed class OwnerWindowEventTests
{
    private const string Reference = "from=16;to=26;tip=3;digest=ab";

    [Theory]
    [InlineData(26)]
    [InlineData(1)]
    public void TheOpeningIsAnAdministrativeActOfTheSystemOnTheChainWithTheVersionReachedAsTheItemIdAndTheReference(int to)
    {
        var opened = CustodyEvent.OwnerWindowOpened(to, Reference);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, to), CustodyStep.OwnerWindowOpened, CustodyReason.AdministrativeAct, Reference, null),
            (opened.Author, opened.Item, opened.Step, opened.Reason, opened.Reference, opened.Corrects));
    }

    [Fact]
    public void TheClosingWithEveryGuaranteeHeldIsAPassedVerificationWithAnEmptyMaskAndTheReference()
    {
        var closed = CustodyEvent.OwnerWindowClosed(0, Reference);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 0), CustodyStep.OwnerWindowClosed, CustodyReason.VerificationPassed, Reference, null),
            (closed.Author, closed.Item, closed.Step, closed.Reason, closed.Reference, closed.Corrects));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void TheClosingWithAGuaranteeMissingIsAFailedVerificationCarryingTheMaskAsTheItemIdAndTheReference(long missing)
    {
        var closed = CustodyEvent.OwnerWindowClosed(missing, Reference);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, missing), CustodyStep.OwnerWindowClosed, CustodyReason.VerificationFailed, Reference, null),
            (closed.Author, closed.Item, closed.Step, closed.Reason, closed.Reference, closed.Corrects));
    }

    [Fact]
    public void ANegativeMaskIsRefusedByTheMissingParameter()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.OwnerWindowClosed(-1, Reference));

        Assert.Equal("missing", refused.ParamName);
    }

    [Theory]
    [InlineData(CustodyStep.OwnerWindowOpened)]
    [InlineData(CustodyStep.OwnerWindowClosed)]
    public void TheTwoStepsHaveAnEventOfTheirOwnAndAreRefusedAsAPlainOne(CustodyStep step) =>
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 1), step, CustodyReason.AdministrativeAct));
}
