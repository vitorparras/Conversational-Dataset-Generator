using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The events of a batch of ingestion (issue 74): its opening by a person, and its closing, accepted or rejected with a motive that is a name and never free text.</summary>
public sealed class CustodyBatchEventTests
{
    private static readonly CustodyAuthor Person = CustodyAuthor.Person(7);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 12)]
    public void TheOpeningIsThePersonsAboutThatBatchForAnAdministrativeActWithNoReference(long personId, long batchId)
    {
        var opened = CustodyEvent.BatchOpened(personId, batchId);

        Assert.Equal(
            (CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.IngestionBatch, batchId), CustodyStep.BatchOpened, CustodyReason.AdministrativeAct, null, null),
            (opened.Author, opened.Item, opened.Step, opened.Reason, opened.Reference, opened.Corrects));
    }

    [Fact]
    public void TheOpeningWithoutAPositivePersonOrBatchNumberIsRefusedByTheParameter()
    {
        var person = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.BatchOpened(0, 1));
        var negativePerson = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.BatchOpened(-1, 1));
        var batch = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.BatchOpened(1, 0));
        var negativeBatch = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.BatchOpened(1, -1));

        Assert.Equal(("personId", "personId", "batchId", "batchId"), (person.ParamName, negativePerson.ParamName, batch.ParamName, negativeBatch.ParamName));
    }

    [Fact]
    public void TheAcceptanceIsTheAuthorsAboutThatBatchForTheReasonAcceptedWithNoReference()
    {
        var byPerson = CustodyEvent.BatchAccepted(Person, 5);
        var bySystem = CustodyEvent.BatchAccepted(CustodyAuthor.System, 6);

        Assert.Equal(
            (
                (Person, new CustodyItem(CustodyItemKind.IngestionBatch, 5), CustodyStep.BatchClosed, CustodyReason.BatchAccepted, null, null),
                (CustodyAuthor.System, new CustodyItem(CustodyItemKind.IngestionBatch, 6), CustodyStep.BatchClosed, CustodyReason.BatchAccepted, null, null)),
            (
                (byPerson.Author, byPerson.Item, byPerson.Step, byPerson.Reason, byPerson.Reference, byPerson.Corrects),
                (bySystem.Author, bySystem.Item, bySystem.Step, bySystem.Reason, bySystem.Reference, bySystem.Corrects)));
    }

    [Fact]
    public void TheAcceptanceWithoutAnAuthorOrAPositiveBatchNumberIsRefusedByTheParameter()
    {
        var author = Assert.Throws<ArgumentNullException>(() => CustodyEvent.BatchAccepted(null!, 1));
        var batch = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.BatchAccepted(Person, 0));
        var negativeBatch = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.BatchAccepted(Person, -1));

        Assert.Equal(("author", "batchId", "batchId"), (author.ParamName, batch.ParamName, negativeBatch.ParamName));
    }

    [Theory]
    [InlineData("invalid_rows")]
    [InlineData("a")]
    [InlineData("write_failed_2")]
    public void TheRejectionIsTheAuthorsAboutThatBatchForTheReasonRejectedAndTheMotiveIsTheReference(string motive)
    {
        var byPerson = CustodyEvent.BatchRejected(Person, 5, motive);
        var bySystem = CustodyEvent.BatchRejected(CustodyAuthor.System, 6, motive);

        Assert.Equal(
            (
                (Person, new CustodyItem(CustodyItemKind.IngestionBatch, 5), CustodyStep.BatchClosed, CustodyReason.BatchRejected, motive, null),
                (CustodyAuthor.System, new CustodyItem(CustodyItemKind.IngestionBatch, 6), CustodyStep.BatchClosed, CustodyReason.BatchRejected, motive, null)),
            (
                (byPerson.Author, byPerson.Item, byPerson.Step, byPerson.Reason, byPerson.Reference, byPerson.Corrects),
                (bySystem.Author, bySystem.Item, bySystem.Step, bySystem.Reason, bySystem.Reference, bySystem.Corrects)));
    }

    [Fact]
    public void TheRejectionWithoutAnAuthorOrAPositiveBatchNumberIsRefusedByTheParameter()
    {
        var author = Assert.Throws<ArgumentNullException>(() => CustodyEvent.BatchRejected(null!, 1, "invalid_rows"));
        var batch = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.BatchRejected(Person, 0, "invalid_rows"));
        var negativeBatch = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.BatchRejected(Person, -1, "invalid_rows"));

        Assert.Equal(("author", "batchId", "batchId"), (author.ParamName, batch.ParamName, negativeBatch.ParamName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Invalid_rows")]
    [InlineData("1rows")]
    [InlineData("_rows")]
    [InlineData("has space")]
    [InlineData("with-dash")]
    [InlineData("com_ção")]
    public void TheRejectionWithAMotiveThatIsNotANameIsRefusedByTheMotiveParameter(string? motive)
    {
        var refused = Assert.Throws<ArgumentException>(() => CustodyEvent.BatchRejected(Person, 1, motive!));

        Assert.Equal("motive", refused.ParamName);
    }

    [Fact]
    public void AMotiveIsAcceptedUpToSixtyThreeCharactersAndRefusedFromSixtyFour()
    {
        var longest = "a" + new string('b', 62);
        var tooLong = "a" + new string('b', 63);

        Assert.Equal((true, false), (CustodyEvent.IsBatchMotive(longest), CustodyEvent.IsBatchMotive(tooLong)));
        Assert.Equal(longest, CustodyEvent.BatchRejected(Person, 1, longest).Reference);
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.BatchRejected(Person, 1, tooLong));
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("z9_", true)]
    [InlineData("a_b_c", true)]
    [InlineData("0", false)]
    [InlineData("A", false)]
    [InlineData("aB", false)]
    [InlineData("a b", false)]
    [InlineData("a-b", false)]
    [InlineData("a.b", false)]
    [InlineData("a/", false)]
    [InlineData("a{", false)]
    [InlineData("a`", false)]
    [InlineData("a@", false)]
    [InlineData("a:", false)]
    [InlineData("aé", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AMotiveIsALowercaseLetterThenLowercaseLettersDigitsAndUnderscoresOnly(string? motive, bool expected) =>
        Assert.Equal(expected, CustodyEvent.IsBatchMotive(motive));

    [Theory]
    [InlineData(CustodyStep.BatchOpened, CustodyReason.AdministrativeAct)]
    [InlineData(CustodyStep.BatchClosed, CustodyReason.BatchAccepted)]
    [InlineData(CustodyStep.BatchClosed, CustodyReason.BatchRejected)]
    public void TheEventsOfABatchAsPlainEventsAreRefused(CustodyStep step, CustodyReason reason) =>
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(Person, new CustodyItem(CustodyItemKind.IngestionBatch, 9), step, reason));
}
