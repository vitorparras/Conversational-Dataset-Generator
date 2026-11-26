using Core.Boundary.Contracts;
using Core.Boundary.Text;
using Core.Host.Submissions;
using Core.Host.Tests.Analysis;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Content.Writing;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The first pseudonymized text of an item stored before the coreference (issue 102, F3): the service is asked once for the raw texts of its batch, with no author, and gives only the
/// groups; each occurrence held takes the group of its pair and keeps its destination, but for the place a slice before A preserved, which becomes a marker (0056). An occurrence held
/// with no pair refuses the item with the reason, and a service that does not answer leaves it unanswered.
/// </summary>
public sealed class RerunAnswersTests
{
    private const string Place = "ZQXCIDADE";
    private const string Raw = Place + " fica longe";
    private const string Other = "outro texto zqx";

    [Fact]
    public async Task PreservedPlaceOfAnOldItemBecomesAMarker()
    {
        var recognizer = Answering(new Occurrence(0, Place.Length, OccurrenceKinds.Location, "PROPN", "nsubj"));

        var answer = await AnswerAsync(recognizer, Old(OccurrenceDestination.Preserve));

        var produced = Assert.IsType<Resanitized>(answer.Produced);
        var marker = Assert.Single(produced.Map!).Marker;
        Assert.Equal((true, $"{marker} fica longe"), (marker.StartsWith("LUGAR_", StringComparison.Ordinal), produced.Pseudonymized));
        Assert.Equal([$"{marker}:{Place}:False:{OccurrenceDestination.Pseudonymize}"], produced.Map!.Select(entry => $"{entry.Marker}:{entry.Original}:{entry.Author}:{entry.Destination}"));
        Assert.Null(answer.Refusal);
    }

    [Theory]
    [InlineData("ORGANIZATION", OccurrenceDestination.Preserve, true)]
    [InlineData("PERSON", OccurrenceDestination.Preserve, true)]
    [InlineData("LOCATION", OccurrenceDestination.Remove, false)]
    public async Task EveryOtherDestinationOfAnOldItemIsKept(string kind, OccurrenceDestination destination, bool kept)
    {
        var held = Old(destination) with { Occurrences = [new ItemOccurrence(0, Place.Length, kind, destination)] };

        var answer = await AnswerAsync(Answering(new Occurrence(0, Place.Length, kind, "PROPN", "nsubj")), held);

        var produced = Assert.IsType<Resanitized>(answer.Produced);
        Assert.Equal((0, kept), (produced.Map!.Count, produced.Pseudonymized!.Contains(Place, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task OldItemIsAskedOnceWithTheTextsOfItsBatchAndNoAuthor()
    {
        var recognizer = Answering(new Occurrence(0, Place.Length, OccurrenceKinds.Location, "PROPN", "nsubj"));

        _ = await AnswerAsync(recognizer, Old(OccurrenceDestination.Pseudonymize) with { Batch = [Other, Raw], Position = 1 });

        var asked = Assert.Single(recognizer.AskedItems);
        Assert.Equal([new ItemText(Other, null), new ItemText(Raw, null)], asked.Texts);
        Assert.Equal((SubmissionAnalysis.Language, ContractVersion.Current), (asked.Language, asked.ContractVersion));
    }

    [Theory]
    [InlineData(1, 9, "LOCATION")]
    [InlineData(0, 8, "LOCATION")]
    [InlineData(0, 9, "PERSON")]
    public async Task OldItemWithAnOccurrenceWithoutPairIsRefusedWithTheReason(int start, int end, string kind)
    {
        var answer = await AnswerAsync(Answering(new Occurrence(start, end, kind, "PROPN", "nsubj")), Old(OccurrenceDestination.Preserve));

        Assert.Equal(new RerunAnswer(null, RerunRefusal.PseudonymWithoutPair), answer);
    }

    [Fact]
    public async Task OldItemTheServiceDoesNotAnswerIsUnanswered()
    {
        var answer = await AnswerAsync(AnsweringRecognizer.Unavailable(UnavailabilityReason.TimedOut), Old(OccurrenceDestination.Preserve));

        Assert.Equal(RerunAnswer.Unanswered, answer);
    }

    [Fact]
    public async Task ItemThatHoldsAMapIsRerunWithoutAskingTheService()
    {
        var recognizer = Answering();
        var held = Old(OccurrenceDestination.Pseudonymize) with { Batch = null, Map = [new NewMapEntry("LUGAR_00000003", Place, false, OccurrenceDestination.Pseudonymize)] };

        var answer = await AnswerAsync(recognizer, held);

        Assert.Equal(("LUGAR_00000003 fica longe", 0), (answer.Produced?.Pseudonymized, recognizer.AskedItems.Count));
    }

    /// <summary>An item of <see cref="Raw"/> stored with no map, its place held with the destination given, alone in its batch.</summary>
    private static RerunSource Old(OccurrenceDestination place) =>
        new(Raw, [new ItemOccurrence(0, Place.Length, OccurrenceKinds.Location, place)], "texto antigo zqx", null) { ItemId = 7, Batch = [Raw], Position = 0 };

    private static AnsweringRecognizer Answering(params Occurrence[] found) => new(RecognitionOutcome.Analyzed(new AnalysisResponse(found, "zqx-model-0102")));

    private static Task<RerunAnswer> AnswerAsync(AnsweringRecognizer recognizer, RerunSource held) => RerunAnswers.OfAsync(
        (new SanitizationRerun(new Sanitizer(CompositionRoot.StepsOf(), new TextBoundaries())), CompositionRoot.SwapOf()), () => recognizer, held, TestContext.Current.CancellationToken);
}
