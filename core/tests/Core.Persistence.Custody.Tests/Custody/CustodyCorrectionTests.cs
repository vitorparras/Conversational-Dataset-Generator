using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Custody;

/// <summary>A correction is an event of its own, with its author and its moment, that points at the one it corrects, which stays in the sequence exactly as it was (#58, CA-4).</summary>
[Trait("Category", "Database")]
public sealed class CustodyCorrectionTests : IClassFixture<CustodyDatabase>
{
    private readonly CustodyDatabase database;

    public CustodyCorrectionTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task CorrectionHasItsOwnAuthorAndComesAfterTheEventItCorrects()
    {
        var chain = await database.ChainAsync();
        var author = await database.PersonAsync(active: true);
        var corrected = await chain.AppendAsync(Event(CustodyAuthor.System, 10101), TestContext.Current.CancellationToken);

        var correction = await chain.AppendAsync(CustodyEvent.Correction(CustodyAuthor.Person(author), Item(10101), corrected.Sequence!.Value), TestContext.Current.CancellationToken);

        var rows = await database.EventsAsync();
        var (before, after) = (Row(rows, corrected), Row(rows, correction));
        Assert.Equal((author, false, corrected.Sequence), (after.ActorPersonId, after.ActorSystem, after.Corrects));
        Assert.True(after.Sequence > before.Sequence);
        Assert.True(after.OccurredAt >= before.OccurredAt);
        Assert.Equal(before.Digest, after.PreviousDigest);
    }

    [DatabaseFact]
    public async Task CorrectedEventIsLeftExactlyAsItWasInEveryColumn()
    {
        var chain = await database.ChainAsync();
        var corrected = await chain.AppendAsync(Event(CustodyAuthor.System, 10201), TestContext.Current.CancellationToken);
        var before = Shape(Row(await database.EventsAsync(), corrected));

        _ = await chain.AppendAsync(CustodyEvent.Correction(CustodyAuthor.System, Item(10201), corrected.Sequence!.Value), TestContext.Current.CancellationToken);

        Assert.Equal(before, Shape(Row(await database.EventsAsync(), corrected)));
    }

    [DatabaseFact]
    public async Task CorrectionIsOneMoreEventAndTheChainStaysLinked()
    {
        var chain = await database.ChainAsync();
        var corrected = await chain.AppendAsync(Event(CustodyAuthor.System, 10301), TestContext.Current.CancellationToken);
        var count = (await database.EventsAsync()).Count;

        _ = await chain.AppendAsync(CustodyEvent.Correction(CustodyAuthor.System, Item(10301), corrected.Sequence!.Value), TestContext.Current.CancellationToken);

        var rows = await database.EventsAsync();
        Assert.Equal(count + 1, rows.Count);
        ChainIntegrity.Holds(rows);
    }

    [DatabaseFact]
    public async Task CorrectionCanItselfBeCorrectedAndBothStayInTheSequence()
    {
        var chain = await database.ChainAsync();
        var corrected = await chain.AppendAsync(Event(CustodyAuthor.System, 10401), TestContext.Current.CancellationToken);
        var first = await chain.AppendAsync(CustodyEvent.Correction(CustodyAuthor.System, Item(10401), corrected.Sequence!.Value), TestContext.Current.CancellationToken);

        var second = await chain.AppendAsync(CustodyEvent.Correction(CustodyAuthor.System, Item(10401), first.Sequence!.Value), TestContext.Current.CancellationToken);

        var rows = await database.EventsAsync();
        Assert.Equal(CustodyOutcome.Appended, second.Outcome);
        Assert.Equal(first.Sequence, Row(rows, second).Corrects);
        Assert.Equal(corrected.Sequence, Row(rows, first).Corrects);
        ChainIntegrity.Holds(rows);
    }

    [DatabaseFact]
    public async Task CorrectionSignedByADeactivatedPersonIsRefusedAndNothingIsWritten()
    {
        var chain = await database.ChainAsync();
        var corrected = await chain.AppendAsync(Event(CustodyAuthor.System, 10501), TestContext.Current.CancellationToken);
        var count = (await database.EventsAsync()).Count;

        var refused = await chain.AppendAsync(
            CustodyEvent.Correction(CustodyAuthor.Person(await database.PersonAsync(active: false)), Item(10501), corrected.Sequence!.Value),
            TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.ActorDeactivated, refused.Outcome);
        Assert.Equal(count, (await database.EventsAsync()).Count);
    }

    private static string Shape(EventRow row) =>
        string.Join(
            '|',
            row.Sequence,
            row.OccurredAt.UtcTicks,
            row.ActorPersonId,
            row.ActorSystem,
            row.ItemKind,
            row.ItemId,
            row.Step,
            row.Reason,
            row.Corrects,
            Convert.ToHexString(row.PreviousDigest),
            Convert.ToHexString(row.Digest),
            row.RecordedBy);

    private static EventRow Row(IEnumerable<EventRow> rows, CustodyResult result) => rows.Single(row => row.Sequence == result.Sequence);

    private static CustodyItem Item(long id) => new(CustodyItemKind.Person, id);

    private static CustodyEvent Event(CustodyAuthor author, long item) =>
        CustodyEvent.Of(author, Item(item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
}
