using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The events of an emptying before they reach the server: who may sign them, which reasons each takes, and what a reference may be.</summary>
public sealed class CustodyEmptyingEventTests
{
    private const string Protocol = "PROT-2026/0042";

    [Theory]
    [InlineData(CustodyReason.RemovalRequestedBySubject)]
    [InlineData(CustodyReason.ContractRequirement)]
    public void EmptyingCarriesTheItemTheReasonTheAuthorAndTheReference(CustodyReason reason)
    {
        var custodyEvent = CustodyEvent.Emptied(CustodyAuthor.Person(5), 9, reason, Protocol);

        Assert.Equal(
            (CustodyAuthor.Person(5), new CustodyItem(CustodyItemKind.ItemText, 9), CustodyStep.ItemEmptied, reason, Protocol),
            (custodyEvent.Author, custodyEvent.Item, custodyEvent.Step, custodyEvent.Reason, custodyEvent.Reference));
    }

    [Fact]
    public void EmptyingDoneByTheSystemIsRefused() =>
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Emptied(CustodyAuthor.System, 9, CustodyReason.ContractRequirement, Protocol));

    [Theory]
    [InlineData(CustodyReason.AuthorWithoutFunction)]
    [InlineData(CustodyReason.ItemAlreadyEmptied)]
    [InlineData(CustodyReason.ContentRead)]
    [InlineData(CustodyReason.AdministrativeAct)]
    public void EmptyingWithAReasonThatIsNotAMotiveIsRefused(CustodyReason reason) =>
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Emptied(CustodyAuthor.Person(5), 9, reason, Protocol));

    [Theory]
    [InlineData(CustodyReason.AuthorWithoutFunction)]
    [InlineData(CustodyReason.ItemAlreadyEmptied)]
    public void RefusalCarriesTheReasonItWasRefusedForAndMayBeSignedByTheSystem(CustodyReason reason)
    {
        var custodyEvent = CustodyEvent.EmptyingRefused(CustodyAuthor.System, 9, reason, Protocol);

        Assert.Equal((CustodyStep.ItemEmptyingRefused, reason, Protocol), (custodyEvent.Step, custodyEvent.Reason, custodyEvent.Reference));
    }

    [Theory]
    [InlineData(CustodyReason.RemovalRequestedBySubject)]
    [InlineData(CustodyReason.ContractRequirement)]
    [InlineData(CustodyReason.ContentRead)]
    public void RefusalWithAReasonThatIsNotARefusalIsRefused(CustodyReason reason) =>
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.EmptyingRefused(CustodyAuthor.Person(5), 9, reason, Protocol));

    [Theory]
    [InlineData(CustodyStep.ItemEmptied)]
    [InlineData(CustodyStep.ItemEmptyingRefused)]
    public void EmptyingStepsAsAPlainEventAreRefused(CustodyStep step) =>
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(CustodyAuthor.Person(5), new CustodyItem(CustodyItemKind.ItemText, 9), step, CustodyReason.ContentRead));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void EmptyingOfAnItemWithoutAPositiveIdentifierIsRefused(long item) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.Emptied(CustodyAuthor.Person(5), item, CustodyReason.ContractRequirement, Protocol));

    [Theory]
    [InlineData("")]
    [InlineData("Maria da Silva")]
    [InlineData("protocolo\n42")]
    [InlineData("ação-42")]
    [InlineData("a;b")]
    public void ReferenceThatIsNotAProtocolNumberIsRefused(string reference) =>
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Emptied(CustodyAuthor.Person(5), 9, CustodyReason.ContractRequirement, reference));

    [Theory]
    [InlineData("1")]
    [InlineData("PROT-2026/0042.a_b")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ012345._/-")]
    public void ReferenceOfLettersDigitsAndTheFourSignsIsAccepted(string reference) =>
        Assert.Equal(reference, CustodyEvent.Emptied(CustodyAuthor.Person(5), 9, CustodyReason.ContractRequirement, reference).Reference);

    [Fact]
    public void ReferenceOf64CharactersIsAcceptedAndOf65IsNot()
    {
        var accepted = CustodyEvent.Emptied(CustodyAuthor.Person(5), 9, CustodyReason.ContractRequirement, new string('a', 64));

        Assert.Equal(64, accepted.Reference?.Length);
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Emptied(CustodyAuthor.Person(5), 9, CustodyReason.ContractRequirement, new string('a', 65)));
    }

    [Fact]
    public void EventThatIsNotAnEmptyingCarriesNoReference() =>
        Assert.Null(CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct).Reference);
}
