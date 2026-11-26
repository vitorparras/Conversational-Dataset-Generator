using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;

namespace Core.Ingestion.Tests.Deciding;

/// <summary>
/// The item analysed whole is decided as each text was: every occurrence has its destination, in its own text, and keeps the coreference group the service gave it, with the group of
/// each author (decision 0060); an item the service did not analyse is the refusal and never an item decided as clean (issue 102).
/// </summary>
public sealed class DecidedItemTests
{
    private static readonly ItemAnalysisResponse Analysed = new(
        [
            new GroupedOccurrence(0, 0, 4, OccurrenceKinds.Person, "PROPN", "nsubj", 1),
            new GroupedOccurrence(1, 5, 9, OccurrenceKinds.Person, "PROPN", "obj", 1),
            new GroupedOccurrence(1, 10, 18, "SECRET", "NOUN", "obj", 2),
        ],
        [new AuthorGroup("ZQXUM", 1), new AuthorGroup("ZQXDOIS", 3)],
        ModelVersion: null);

    private static readonly Material Conversation = Found.Accepted(MaterialForm.Conversation, Units.Message("ZQXA oi", "ZQXUM", 1), Units.Message("viu ZQXA senha123", "ZQXDOIS", 2));

    [Fact]
    public void EveryOccurrenceLandsInItsOwnTextWithItsDestinationAndItsGroup()
    {
        var decided = DecidedMaterial.Decide(Conversation, ItemRecognitionOutcome.Analyzed(Analysed)).Decided!;

        Assert.Equal([new DecidedOccurrence(new Occurrence(0, 4, OccurrenceKinds.Person, "PROPN", "nsubj"), Destination.Pseudonymize) { Group = 1 }], decided.Of(0));
        Assert.Equal(
            [
                new DecidedOccurrence(new Occurrence(5, 9, OccurrenceKinds.Person, "PROPN", "obj"), Destination.Pseudonymize) { Group = 1 },
                new DecidedOccurrence(new Occurrence(10, 18, "SECRET", "NOUN", "obj"), Destination.Remove) { Group = 2 },
            ],
            decided.Of(1));
    }

    [Fact]
    public void TheGroupOfEachAuthorComesThroughAsTheServiceGaveIt()
    {
        var decided = DecidedMaterial.Decide(Conversation, ItemRecognitionOutcome.Analyzed(Analysed)).Decided!;

        Assert.Equal([new AuthorGroup("ZQXUM", 1), new AuthorGroup("ZQXDOIS", 3)], decided.AuthorGroups);
    }

    [Fact]
    public void AnItemAnalysedWithNothingFoundHasEveryTextEmpty()
    {
        var decided = DecidedMaterial.Decide(Conversation, ItemRecognitionOutcome.Analyzed(new ItemAnalysisResponse([], [], null))).Decided!;

        Assert.Equal((0, 0, 0), (decided.Of(0).Count, decided.Of(1).Count, decided.AuthorGroups.Count));
    }

    [Theory]
    [InlineData(UnavailabilityReason.TimedOut)]
    [InlineData(UnavailabilityReason.MalformedResponse)]
    public void AnItemTheServiceDidNotAnalyseIsTheRefusalAndNeverAnItemDecidedAsClean(UnavailabilityReason reason)
    {
        var result = DecidedMaterial.Decide(Conversation, ItemRecognitionOutcome.Unavailable(reason));

        Assert.Equal((null, reason), (result.Decided, result.Reason));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void AnOccurrenceInATextTheItemDoesNotHaveIsADefect(int text)
    {
        var outside = new ItemAnalysisResponse([new GroupedOccurrence(text, 0, 1, OccurrenceKinds.Person, "PROPN", "nsubj", 1)], [], null);

        var defect = Assert.Throws<ArgumentException>(() => DecidedMaterial.Decide(Conversation, ItemRecognitionOutcome.Analyzed(outside)));

        Assert.Equal("analysis", defect.ParamName);
    }

    [Fact]
    public void NoItemAndNoAnalysisAreDefects()
    {
        var analysis = ItemRecognitionOutcome.Analyzed(Analysed);

        Assert.Equal("material", Assert.Throws<ArgumentNullException>(() => DecidedMaterial.Decide(null!, analysis)).ParamName);
        Assert.Equal("analysis", Assert.Throws<ArgumentNullException>(() => DecidedMaterial.Decide(Conversation, (ItemRecognitionOutcome)null!)).ParamName);
    }

    [Fact]
    public void TheTextsAnalysedOneByOneHaveNoGroupAndNoAuthor()
    {
        var material = Found.Accepted(MaterialForm.Comment, Units.Message("ZQXA", "ZQXUM", null));

        var decided = DecidedMaterial.Decide(material, [Found.Analysis(Found.Occurrence(OccurrenceKinds.Person, "PROPN", "nsubj"))]).Decided!;

        Assert.Equal((null, 0), (decided.Of(0)[0].Group, decided.AuthorGroups.Count));
    }
}
