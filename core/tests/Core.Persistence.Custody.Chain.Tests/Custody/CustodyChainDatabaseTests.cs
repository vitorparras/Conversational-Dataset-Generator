using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Custody;

/// <summary>The chain against a real server, as the application's role (#58, CA-1, CA-4 and CA-6): what an event is stored as, how events link, and who may sign.</summary>
[Trait("Category", "Database")]
public sealed class CustodyChainDatabaseTests : IClassFixture<CustodyDatabase>
{
    private static readonly byte[] Origin = new byte[32];

    private readonly CustodyDatabase database;

    public CustodyChainDatabaseTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task EventIsStoredWithTheNamesOfItsItemStepAndReasonAndTheSystemAsAuthor()
    {
        var chain = await database.ChainAsync();

        var result = await chain.AppendAsync(Event(CustodyAuthor.System, 4101), TestContext.Current.CancellationToken);

        var row = (await database.EventsAsync()).Single(candidate => candidate.Sequence == result.Sequence);
        Assert.Equal(CustodyOutcome.Appended, result.Outcome);
        Assert.Equal(("person", 4101L, "person_created", "administrative_act"), (row.ItemKind, row.ItemId, row.Step, row.Reason));
        Assert.Equal((null, true, null), (row.ActorPersonId, row.ActorSystem, row.Corrects));
    }

    [DatabaseFact]
    public async Task EventsAppendedOneByOneFormAChainInWhichEachPointsAtTheDigestOfThePreviousOne()
    {
        var chain = await database.ChainAsync();
        foreach (var item in new long[] { 4201, 4202, 4203, 4204, 4205 })
        {
            _ = await chain.AppendAsync(Event(CustodyAuthor.System, item), TestContext.Current.CancellationToken);
        }

        var rows = await database.EventsAsync();

        Assert.Equal(Origin, rows[0].PreviousDigest);
        Assert.Equal(Enumerable.Range(1, rows.Count).Select(number => (long)number), rows.Select(row => row.Sequence));
        Assert.All(rows.Zip(rows.Skip(1)), pair => Assert.Equal(pair.First.Digest, pair.Second.PreviousDigest));
        Assert.Equal(rows.Count, rows.Select(row => Convert.ToHexString(row.PreviousDigest)).Distinct(StringComparer.Ordinal).Count());
    }

    [DatabaseFact]
    public async Task DigestOfEveryEventIsTheOneWorkedOutFromItsRowByTheDeclaredEncoding()
    {
        var chain = await database.ChainAsync();
        _ = await chain.AppendAsync(Event(CustodyAuthor.Person(await database.PersonAsync(active: true)), 4301), TestContext.Current.CancellationToken);
        _ = await chain.AppendAsync(Event(CustodyAuthor.System, 4302), TestContext.Current.CancellationToken);

        var rows = await database.EventsAsync();

        Assert.All(rows, row => Assert.Equal(CanonicalDigest.Of(row), row.Digest));
    }

    [DatabaseFact]
    public async Task ActivePersonSignsAsThemselvesAndNotAsTheSystem()
    {
        var chain = await database.ChainAsync();
        var person = await database.PersonAsync(active: true);

        var result = await chain.AppendAsync(Event(CustodyAuthor.Person(person), 4401), TestContext.Current.CancellationToken);

        var row = (await database.EventsAsync()).Single(candidate => candidate.Sequence == result.Sequence);
        Assert.Equal((person, false), (row.ActorPersonId, row.ActorSystem));
    }

    [DatabaseFact]
    public async Task DeactivatedAuthorIsRefusedAndNothingIsWritten()
    {
        var chain = await database.ChainAsync();
        var person = await database.PersonAsync(active: false);
        var before = (await database.EventsAsync()).Count;

        var result = await chain.AppendAsync(Event(CustodyAuthor.Person(person), 4501), TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.ActorDeactivated, result.Outcome);
        Assert.NotNull(result.Refusal);
        Assert.Null(result.Sequence);
        Assert.Equal(before, (await database.EventsAsync()).Count);
    }

    [DatabaseFact]
    public async Task AuthorWhoIsNotRegisteredIsRefusedAndNothingIsWritten()
    {
        var chain = await database.ChainAsync();
        var before = (await database.EventsAsync()).Count;

        var result = await chain.AppendAsync(Event(CustodyAuthor.Person(long.MaxValue), 4601), TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.ActorUnknown, result.Outcome);
        Assert.Equal(before, (await database.EventsAsync()).Count);
    }

    [DatabaseFact]
    public async Task CorrectionIsAnEventOfItsOwnThatPointsAtTheCorrectedOneAndLeavesItInTheSequence()
    {
        var chain = await database.ChainAsync();
        var corrected = await chain.AppendAsync(Event(CustodyAuthor.System, 4701), TestContext.Current.CancellationToken);
        var before = (await database.EventsAsync()).Single(row => row.Sequence == corrected.Sequence);

        var correction = await chain.AppendAsync(
            CustodyEvent.Correction(CustodyAuthor.Person(await database.PersonAsync(active: true)), Item(4701), corrected.Sequence!.Value),
            TestContext.Current.CancellationToken);

        var rows = await database.EventsAsync();
        var written = rows.Single(row => row.Sequence == correction.Sequence);
        Assert.Equal((corrected.Sequence, "correction", "correction_of_record"), (written.Corrects, written.Step, written.Reason));
        _ = Assert.NotNull(written.ActorPersonId);
        Assert.Equal(before.Digest, rows.Single(row => row.Sequence == corrected.Sequence).Digest);
    }

    [DatabaseFact]
    public async Task CorrectionOfAnEventThatIsNotInTheChainIsRefusedAndNothingIsWritten()
    {
        var chain = await database.ChainAsync();
        var before = (await database.EventsAsync()).Count;

        var result = await chain.AppendAsync(CustodyEvent.Correction(CustodyAuthor.System, Item(4801), long.MaxValue), TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.CorrectedNotFound, result.Outcome);
        Assert.Equal(before, (await database.EventsAsync()).Count);
    }

    private static CustodyItem Item(long id) => new(CustodyItemKind.Person, id);

    private static CustodyEvent Event(CustodyAuthor author, long item) =>
        CustodyEvent.Of(author, Item(item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
}
