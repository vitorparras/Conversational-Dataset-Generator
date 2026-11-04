using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The event of the result of a rehearsal of a restore (issue 66, CA-2 and CA-3): about the chain, by the system, passed or failed, with the mask in the id and the names in the reference.</summary>
public sealed class CustodyRehearsalEventTests
{
    [Fact]
    public void ResultThatHeldIsPassedAboutTheChainWithTheMaskZeroAndTheReferenceHeld()
    {
        var custodyEvent = CustodyEvent.RestoreRehearsed(0, "held");

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 0), CustodyStep.RestoreRehearsed, CustodyReason.VerificationPassed, "held", null),
            (custodyEvent.Author, custodyEvent.Item, custodyEvent.Step, custodyEvent.Reason, custodyEvent.Reference, custodyEvent.Corrects));
    }

    [Fact]
    public void ResultThatDidNotHoldIsFailedWithTheMaskAndTheNamesOfWhatFailed()
    {
        var custodyEvent = CustodyEvent.RestoreRehearsed(130, "policy.seal");

        Assert.Equal((CustodyReason.VerificationFailed, 130L, "policy.seal"), (custodyEvent.Reason, custodyEvent.Item.Id, custodyEvent.Reference));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void MaskOutsideTheClosedListIsRefused(long mask) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.RestoreRehearsed(mask, "held"));

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    [InlineData("a;b")]
    public void NamesThatAreNotAReferenceAreRefused(string names) =>
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.RestoreRehearsed(1, names));

    [Fact]
    public void ResultAsAPlainEventIsRefused() =>
        _ = Assert.Throws<ArgumentException>(
            () => CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 0), CustodyStep.RestoreRehearsed, CustodyReason.VerificationPassed));

    [Fact]
    public void TheStepIsWrittenUnderItsName() =>
        Assert.Equal("restore_rehearsed", CustodyNames.Of(CustodyStep.RestoreRehearsed));
}
