using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The event of a seal recorded (issue 62, CA-2): about the chain, by the system or by a person, for an administrative act, and in no other shape.</summary>
public sealed class CustodySealEventTests
{
    [Fact]
    public void RecordOfASealIsAboutTheChainWithTheSequenceSealedAndNoReferenceAndNoCorrection()
    {
        var custodyEvent = CustodyEvent.SealRecorded(CustodyAuthor.System, 42);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 42), CustodyStep.SealRecorded, CustodyReason.AdministrativeAct, null, null),
            (custodyEvent.Author, custodyEvent.Item, custodyEvent.Step, custodyEvent.Reason, custodyEvent.Reference, custodyEvent.Corrects));
    }

    [Fact]
    public void ThePersonWhoConfirmedTheRecordIsTheAuthor() =>
        Assert.Equal(CustodyAuthor.Person(5), CustodyEvent.SealRecorded(CustodyAuthor.Person(5), 1).Author);

    [Fact]
    public void ARecordOfAChainWithNoEventsIsAllowedAndANegativeSequenceIsNot()
    {
        Assert.Equal(0, CustodyEvent.SealRecorded(CustodyAuthor.System, 0).Item.Id);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.SealRecorded(CustodyAuthor.System, -1));
    }

    [Fact]
    public void ARecordWithNoAuthorIsRefused() =>
        _ = Assert.Throws<ArgumentNullException>(() => CustodyEvent.SealRecorded(null!, 1));

    [Fact]
    public void ARecordAsAPlainEventIsRefused() =>
        _ = Assert.Throws<ArgumentException>(
            () => CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, 3), CustodyStep.SealRecorded, CustodyReason.AdministrativeAct));

    [Fact]
    public void TheStepIsWrittenUnderItsName() =>
        Assert.Equal("seal_recorded", CustodyNames.Of(CustodyStep.SealRecorded));
}
