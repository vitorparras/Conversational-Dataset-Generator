using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The event that records a verification: named, written by the system, and carrying no content.</summary>
public sealed class CustodyVerificationEventTests
{
    [Fact]
    public void PassedVerificationIsTheSystemOnTheChainWithTheNumberOfEventsAndNoEventPointedAt()
    {
        var recorded = CustodyEvent.Verification(12, null);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 12), CustodyStep.VerifyChain, CustodyReason.VerificationPassed, null),
            (recorded.Author, recorded.Item, recorded.Step, recorded.Reason, recorded.Corrects));
    }

    [Fact]
    public void FailedVerificationPointsAtTheFirstInconsistentEvent()
    {
        var recorded = CustodyEvent.Verification(12, 5);

        Assert.Equal((CustodyReason.VerificationFailed, 5), (recorded.Reason, recorded.Corrects));
    }

    [Fact]
    public void VerificationOfAnEmptyChainIsAllowedAndNegativeOrZeroReferencesAreNot()
    {
        Assert.Equal(0, CustodyEvent.Verification(0, null).Item.Id);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.Verification(-1, null));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.Verification(3, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.Verification(3, -2));
    }

    [Fact]
    public void ThatStepIsNotWrittenByTheOrdinaryConstructor() =>
        _ = Assert.Throws<ArgumentException>(
            () => CustodyEvent.Of(CustodyAuthor.Person(1), new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.VerifyChain, CustodyReason.AdministrativeAct));

    [Fact]
    public void NamesOfTheVerificationAreTheOnesTheDatabaseListsAndNothingFree()
    {
        Assert.Equal(
            ("custody_chain", "verify_chain", "verification_passed", "verification_failed"),
            (CustodyNames.Of(CustodyItemKind.CustodyChain), CustodyNames.Of(CustodyStep.VerifyChain),
                CustodyNames.Of(CustodyReason.VerificationPassed), CustodyNames.Of(CustodyReason.VerificationFailed)));
    }
}
