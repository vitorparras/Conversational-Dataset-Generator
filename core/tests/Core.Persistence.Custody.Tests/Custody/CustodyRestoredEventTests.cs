using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The event of a restore (#64, CA-5d): written by the system about the chain, with the rows put back as the item and the copy and the tips as the reference.</summary>
public sealed class CustodyRestoredEventTests
{
    [Theory]
    [InlineData(true, CustodyReason.VerificationPassed)]
    [InlineData(false, CustodyReason.VerificationFailed)]
    public void RestoreIsTheSystemsEventAboutTheChainAndTheReasonSaysWhetherEverythingHeld(bool held, CustodyReason reason)
    {
        var restored = CustodyEvent.Restored(1203, held, "copy=abc;before=5;after=7");

        Assert.Equal(
            (CustodyStep.Restored, reason, new CustodyItem(CustodyItemKind.CustodyChain, 1203), "copy=abc;before=5;after=7"),
            (restored.Step, restored.Reason, restored.Item, restored.Reference));
        Assert.Equal(CustodyAuthor.System, restored.Author);
    }

    [Fact]
    public void NegativeCountOfRowsIsRefused()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.Restored(-1, held: true, "copy=abc;before=5;after=7"));
    }

    [Fact]
    public void RestoreCannotBeBuiltAsAnOrdinaryStep()
    {
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 1), CustodyStep.Restored, CustodyReason.VerificationPassed));
    }
}
