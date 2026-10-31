using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>What an event is before it reaches the server: who signs, what it is about, and that a correction is told apart from any other step.</summary>
public sealed class CustodyEventTests
{
    private static readonly CustodyItem Item = new(CustodyItemKind.Person, 7);

    [Fact]
    public void SystemAuthorHasNoPerson() =>
        Assert.Null(CustodyAuthor.System.PersonId);

    [Fact]
    public void PersonAuthorCarriesTheIdentifier() =>
        Assert.Equal(42L, CustodyAuthor.Person(42).PersonId);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PersonAuthorWithoutAPositiveIdentifierIsRefused(long personId) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyAuthor.Person(personId));

    [Fact]
    public void EventCarriesWhatItWasGiven()
    {
        var custodyEvent = CustodyEvent.Of(CustodyAuthor.Person(3), Item, CustodyStep.PersonDeactivated, CustodyReason.AdministrativeAct);

        Assert.Equal(
            (CustodyAuthor.Person(3), Item, CustodyStep.PersonDeactivated, CustodyReason.AdministrativeAct, null),
            (custodyEvent.Author, custodyEvent.Item, custodyEvent.Step, custodyEvent.Reason, custodyEvent.Corrects));
    }

    [Fact]
    public void CorrectionStepAsAPlainEventIsRefused() =>
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(CustodyAuthor.System, Item, CustodyStep.Correction, CustodyReason.CorrectionOfRecord));

    [Fact]
    public void CorrectionStepAsAPlainEventIsRefusedWithTheWayToCorrectItAndTheParameterAtFault()
    {
        var refused = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(CustodyAuthor.System, Item, CustodyStep.Correction, CustodyReason.CorrectionOfRecord));

        Assert.Equal("step", refused.ParamName);
        Assert.Contains("use Correction", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CorrectionNamesTheEventItCorrects()
    {
        var correction = CustodyEvent.Correction(CustodyAuthor.System, Item, 11);

        Assert.Equal((CustodyStep.Correction, CustodyReason.CorrectionOfRecord, 11L), (correction.Step, correction.Reason, correction.Corrects));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CorrectionOfASequenceThatCannotExistIsRefused(long corrected) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.Correction(CustodyAuthor.System, Item, corrected));

    [Fact]
    public void EventWithoutAnAuthorIsRefused() =>
        _ = Assert.Throws<ArgumentNullException>(() => CustodyEvent.Of(null!, Item, CustodyStep.PersonCreated, CustodyReason.AdministrativeAct));

    [Fact]
    public void EventWithoutAnItemIsRefused() =>
        _ = Assert.Throws<ArgumentNullException>(() => CustodyEvent.Of(CustodyAuthor.System, null!, CustodyStep.PersonCreated, CustodyReason.AdministrativeAct));

    [Fact]
    public void CorrectionWithoutAnAuthorIsRefused() =>
        _ = Assert.Throws<ArgumentNullException>(() => CustodyEvent.Correction(null!, Item, 1));

    [Fact]
    public void CorrectionWithoutAnItemIsRefused() =>
        _ = Assert.Throws<ArgumentNullException>(() => CustodyEvent.Correction(CustodyAuthor.System, null!, 1));
}
