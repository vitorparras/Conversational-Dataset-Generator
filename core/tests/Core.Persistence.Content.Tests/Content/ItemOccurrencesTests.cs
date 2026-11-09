using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The destinations of the occurrences of an item are stored with it, sealed, in the statement that adds it, and a later stage reads the destination that was stored and decides nothing
/// (issue 84, CA-4); they never change but by the emptying of the item, which clears them.
/// </summary>
[Trait("Category", "Database")]
public sealed class ItemOccurrencesTests
{
    private const string Raw = "texto bruto sintetico zqx";
    private const string Protocol = "PROT-2026/0042";
    private const string Changed = "CE004";
    private const string Events =
        "SELECT coalesce(string_agg(item_kind || ',' || item_id || ',' || step || ',' || reason || ',' || coalesce(actor_person_id::text, 'system'), ';' ORDER BY sequence), '') "
        + "FROM public.custody_event";

    private static readonly ItemOccurrence[] Decided =
    [
        new(0, 6, "PERSON", OccurrenceDestination.Pseudonymize),
        new(10, 16, "LOCATION", OccurrenceDestination.Preserve),
        new(20, 28, "PASSWORD", OccurrenceDestination.Remove),
    ];

    [DatabaseFact]
    public async Task TheDestinationStoredIsTheOneAStageAfterReads()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Occurrences: Decided), TestContext.Current.CancellationToken);

        var read = await new ItemOccurrenceReader(world.Reader).ReadAsync(CustodyAuthor.System, 1, item, TestContext.Current.CancellationToken);

        Assert.Equal(ContentReadOutcome.Read, read.Reading.Outcome);
        Assert.Equal(Decided, read.Occurrences);
    }

    [DatabaseFact]
    public async Task TheReadingIsAnEventOfTheChainAboutTheItemAndNeverCarriesWhatWasRead()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Occurrences: Decided), TestContext.Current.CancellationToken);

        _ = await new ItemOccurrenceReader(world.Reader).ReadAsync(CustodyAuthor.System, 1, item, TestContext.Current.CancellationToken);

        Assert.Equal($"item_text,{item},occurrences_read,content_read,system", await world.ScalarAsync(Events));
    }

    [DatabaseFact]
    public async Task ATextAnalysedWithNothingFoundIsStoredAsDecidedWithNoOccurrences()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Occurrences: []), TestContext.Current.CancellationToken);

        var read = await new ItemOccurrenceReader(world.Reader).ReadAsync(CustodyAuthor.System, 1, item, TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.Read, 0), (read.Reading.Outcome, read.Occurrences!.Count));
    }

    [DatabaseFact]
    public async Task AnItemWithNoDecisionStoredIsReadAsEmptyAndNeverWithADefaultDestination()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);

        var read = await new ItemOccurrenceReader(world.Reader).ReadAsync(CustodyAuthor.System, 1, item, TestContext.Current.CancellationToken);

        Assert.Equal(ContentReadOutcome.Empty, read.Reading.Outcome);
        Assert.Null(read.Occurrences);
    }

    [DatabaseTheory]
    [InlineData("UPDATE content.item_text SET occurrences = '\\x01' WHERE id = @id")]
    [InlineData("UPDATE content.item_text SET occurrences = NULL WHERE id = @id")]
    public async Task TheOccurrencesOfAnItemNotEmptiedNeverChangeWhoeverWrites(string change)
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Occurrences: Decided), TestContext.Current.CancellationToken);

        var byApplication = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, change, ("id", item)));
        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, change, ("id", item)));

        Assert.Equal((Changed, Changed), (byApplication.SqlState, byOwner.SqlState));
    }

    [DatabaseFact]
    public async Task EmptyingTheItemClearsTheOccurrencesAndTheyAreReadAsEmptied()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Occurrences: Decided), TestContext.Current.CancellationToken);
        var curator = await world.PersonAsync("zqx-curator");

        _ = await world.Emptier.EmptyAsync(
            new ItemEmptying(CustodyAuthor.Person(curator), 1, item, EmptyingMotive.RemovalRequestedBySubject, Protocol),
            TestContext.Current.CancellationToken);
        var read = await new ItemOccurrenceReader(world.Reader).ReadAsync(CustodyAuthor.System, 1, item, TestContext.Current.CancellationToken);

        Assert.Equal(ContentReadOutcome.Emptied, read.Reading.Outcome);
        Assert.Null(read.Occurrences);
        Assert.Equal(0L, await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.item_text WHERE occurrences IS NOT NULL"));
    }

    [DatabaseFact]
    public async Task TheOccurrencesAreStoredSealedAndNeverWithTheKindOrTheDestinationInClear()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Occurrences: Decided), TestContext.Current.CancellationToken);

        var stored = await world.StoredAsync(1, "SELECT occurrences FROM content.item_text WHERE id = @id", item);

        var shown = System.Text.Encoding.UTF8.GetString(stored);
        Assert.Equal([false, false], [shown.Contains("PERSON", StringComparison.Ordinal), shown.Contains("Pseudonymize", StringComparison.Ordinal)]);
    }
}
