using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Host.Submissions;
using Core.Host.Tests.Analysis;
using Core.Ingestion;
using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The submission analyzes the material before it writes the items (#841, CA-1 of #84): each text is asked once, in Portuguese, in the order of the material, and each item is born with
/// the occurrences found in it and the destination of each. With the service of the language gone, a malformed answer or the request cancelled, the batch is rejected with no item.
/// </summary>
public sealed class SubmissionDestinationsTests
{
    private const string AnalysisUnavailable = "analysis_unavailable";
    private const string Earlier = "ZQX-FAKE-PERSON ZQXDOIS ZQX-FAKE-PERSON";
    private const string Later = "ZQXUM ZQX-FAKE-PERSON";

    /// <summary>A conversation whose messages come in the file in the reverse of their instants: the later one first.</summary>
    private const string Reversed =
        /*lang=json*/ """{"formato":"cdg-conversa/1","mensagens":[{"id":"a","remetente":"ZQXUM","instante":"2031-03-04T08:01:00Z","texto":"ZQXUM ZQX-FAKE-PERSON"},""" +
        /*lang=json*/ """{"id":"b","remetente":"ZQXDOIS","instante":"2031-03-04T08:00:00Z","texto":"ZQX-FAKE-PERSON ZQXDOIS ZQX-FAKE-PERSON"}]}""";

    private static readonly int[] Granted = [SubmissionWorld.Domain];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task EachUnitIsAnalyzedOnceInTheOrderOfTheMaterial()
    {
        var recognizer = AnsweringRecognizer.Finding();
        await using var world = await SubmissionWorld.CreateAsync(recognizer: recognizer);

        _ = await world.SubmitAsync(SubmissionWorld.Request(Reversed));

        Assert.Equal([Earlier, Later], recognizer.Asked.Select(request => request.Text));
    }

    [DatabaseFact]
    public async Task EveryAnalysisIsAskedInPortuguese()
    {
        var recognizer = AnsweringRecognizer.Finding();
        await using var world = await SubmissionWorld.CreateAsync(recognizer: recognizer);

        _ = await world.SubmitAsync(SubmissionWorld.Request(Reversed));

        Assert.Equal(["pt", "pt"], recognizer.Asked.Select(request => request.Language));
    }

    [DatabaseFact]
    public async Task EveryItemIsBornWithItsDecidedOccurrences()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: new FakeRecognizer());

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Reversed));

        Assert.Equal(SubmissionKind.Done, outcome.Kind);
        Assert.Equal(
            [(0, 6, 21, "PERSON", OccurrenceDestination.Pseudonymize), (1, 0, 15, "PERSON", OccurrenceDestination.Pseudonymize), (1, 24, 39, "PERSON", OccurrenceDestination.Pseudonymize)],
            await OccurrencesAsync(world, outcome.Batch!.Value));
    }

    [DatabaseFact]
    public async Task AccommodatedItemKeepsItsAccommodationAndItsOccurrences()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var opened = await world.Batches.OpenAsync(new BatchOpening(SubmissionWorld.Domain, SubmissionWorld.Person) { DeclaredForm = "document", SourceForm = "conversation" }, Token);
        var instant = new DateTimeOffset(2031, 3, 4, 8, 0, 0, TimeSpan.Zero);
        var material = new ReadMaterial([new ReadUnit("ZQXTERCEIRO ZQX-FAKE-PERSON", "ZQXUM", instant, null, []), new ReadUnit("ZQX-FAKE-PERSON ZQXQUARTO", "ZQXDOIS", instant, null, [])]);
        var kept = new SubmissionStoring.Kept(SubmissionWorld.Domain, opened.BatchId!.Value, material, MaterialForm.Document, SubmissionWorld.AccommodatedAsDocument(material), SubmissionWorld.Person);
        var conditions = OriginConditions.Of([SubmissionWorld.Strictest, "zqx_media", SubmissionWorld.Loosest]);
        var intake = SubmissionWorld.Intake(1_000_000, conditions, world.Content, world.DataSource, recognizer: new FakeRecognizer());

        var keeping = await new SubmissionStoring.Keeper(world.Batches, intake, NullLogger.Instance).KeepAsync(kept, Token);

        Assert.Equal((SubmissionStoring.Keeping.Kept, 2L), (keeping, await world.AccommodatedRowsAsync()));
        Assert.Equal(["document/author,instant", "document/author,instant"], await world.StoredAsync("SELECT accommodated_into || '/' || losses FROM content.item_text ORDER BY id"));
        Assert.Equal(
            [(0, 12, 27, "PERSON", OccurrenceDestination.Pseudonymize), (1, 0, 15, "PERSON", OccurrenceDestination.Pseudonymize)],
            await OccurrencesAsync(world, opened.BatchId.Value));
    }

    [DatabaseFact]
    public async Task LanguageServiceDownRejectsTheBatchAsAnalysisUnavailable()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: new FakeRecognizer());

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());
        var detail = await world.Service.DetailAsync(Granted, outcome.Batch!.Value, Token);

        Assert.Equal(
            (SubmissionKind.Unavailable, AnalysisUnavailable, BatchState.Rejected, AnalysisUnavailable, 0L),
            (outcome.Kind, outcome.Code, detail!.State, detail.Reason, await world.ItemRowsAsync()));
    }

    [DatabaseFact]
    public async Task MalformedAnalysisRejectsTheBatchAsAnalysisUnavailable()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: AnsweringRecognizer.Unavailable(UnavailabilityReason.MalformedResponse));

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());
        var detail = await world.Service.DetailAsync(Granted, outcome.Batch!.Value, Token);

        Assert.Equal(
            (SubmissionKind.Unavailable, AnalysisUnavailable, BatchState.Rejected, AnalysisUnavailable, 0L),
            (outcome.Kind, outcome.Code, detail!.State, detail.Reason, await world.ItemRowsAsync()));
    }

    [DatabaseFact]
    public async Task AnalysisStopsAtTheFirstTextLeftUnanalyzed()
    {
        var recognizer = new FirstOnlyRecognizer();
        await using var world = await SubmissionWorld.CreateAsync(recognizer: recognizer);
        var content = SubmissionWorld.Messages(("ZQXUM", "ZQXUM ZQX-FAKE-PERSON"), ("ZQXDOIS", "ZQXDOIS ZQX-FAKE-PERSON"), ("ZQXTRES", "ZQXTRES ZQX-FAKE-PERSON"));

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(content));
        var detail = await world.Service.DetailAsync(Granted, outcome.Batch!.Value, Token);

        Assert.Equal(["ZQXUM ZQX-FAKE-PERSON", "ZQXDOIS ZQX-FAKE-PERSON"], recognizer.Asked.Select(request => request.Text));
        Assert.Equal(
            (SubmissionKind.Unavailable, AnalysisUnavailable, BatchState.Rejected, AnalysisUnavailable, 0L),
            (outcome.Kind, outcome.Code, detail!.State, detail.Reason, await world.ItemRowsAsync()));
    }

    [DatabaseFact]
    public async Task CancelledWhileAnalyzingLeavesTheBatchRejectedWithNoItems()
    {
        using var cancelling = new CancellationTokenSource();
        await using var world = await SubmissionWorld.CreateAsync(recognizer: new CancellingRecognizer(cancelling));

        var outcome = await world.Service.SubmitAsync(SubmissionWorld.Request(Reversed), cancelling.Token);
        var detail = await world.Service.DetailAsync(Granted, outcome.Batch!.Value, Token);

        Assert.Equal(
            (true, SubmissionKind.Unavailable, SubmissionStoring.SubmissionCancelled, BatchState.Rejected, SubmissionStoring.SubmissionCancelled, 0L),
            (cancelling.IsCancellationRequested, outcome.Kind, outcome.Code, detail!.State, detail.Reason, await world.ItemRowsAsync()));
    }

    /// <summary>
    /// Every occurrence stored in the world, as the reader of the occurrences gives it once the batch is reviewed and accepted, since the content of a batch still open is read by no one,
    /// by the position of its item in the order the items were written.
    /// </summary>
    private static async Task<List<(int Item, int Start, int End, string Kind, OccurrenceDestination Destination)>> OccurrencesAsync(SubmissionWorld world, long batch)
    {
        await world.AcceptReviewedAsync(batch);
        var reader = new ItemOccurrenceReader(world.Reader);
        var items = await world.StoredAsync("SELECT id::text FROM content.item_text ORDER BY id");
        var found = new List<(int, int, int, string, OccurrenceDestination)>();
        foreach (var (id, position) in items.Select((id, position) => (long.Parse(id, System.Globalization.CultureInfo.InvariantCulture), position)))
        {
            var read = await reader.ReadAsync(CustodyAuthor.Person(SubmissionWorld.Person), SubmissionWorld.Domain, id, Token);
            found.AddRange(read.Occurrences!.Select(occurrence => (position, occurrence.Start, occurrence.End, occurrence.Kind, occurrence.Destination)));
        }

        return found;
    }

    /// <summary>A recognizer that analyzes the first text it is asked for and cannot reach the service for every text after it, keeping the requests it was asked.</summary>
    private sealed class FirstOnlyRecognizer : IRecognizer
    {
        private readonly AnsweringRecognizer first = AnsweringRecognizer.Finding("PERSON");
        private readonly AnsweringRecognizer rest = AnsweringRecognizer.Unavailable(UnavailabilityReason.ConnectionRefused);

        public IEnumerable<AnalysisRequest> Asked => first.Asked.Concat(rest.Asked);

        public Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken) =>
            (first.Asked.Count == 0 ? first : rest).RecognizeAsync(request, cancellationToken);
    }

    /// <summary>A recognizer that cancels the submission when it is first asked, and then answers as the fake one does to a request whose token is cancelled.</summary>
    private sealed class CancellingRecognizer(CancellationTokenSource cancelling) : IRecognizer
    {
        private readonly FakeRecognizer fake = new();

        public async Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken)
        {
            await cancelling.CancelAsync();
            return await fake.RecognizeAsync(request, cancellationToken);
        }
    }
}
