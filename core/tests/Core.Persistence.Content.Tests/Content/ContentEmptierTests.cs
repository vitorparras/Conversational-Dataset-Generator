using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// An item is emptied by the one way that does it (#73): the chain records the emptying and the content is cleared in the same transaction, only a person who holds the function of
/// curation of the collection empties, and a refusal to empty is an event of the chain with the content as it was.
/// </summary>
[Trait("Category", "Database")]
public sealed class ContentEmptierTests
{
    private const string Protocol = "PROT-2026/0042";
    private const string Intact = "false,false,false,false";
    private const string ItemCleared =
        "SELECT (raw_text IS NULL)::text || ',' || (sanitized_text IS NULL)::text || ',' || (pseudonymized_text IS NULL)::text || ',' || (emptied_at IS NOT NULL)::text "
        + "FROM content.item_text WHERE id = @id";
    private const string MapCleared = "SELECT (entries IS NULL)::text || ',' || (emptied_at IS NOT NULL)::text FROM content.reversal_map WHERE item_id = @id";
    private const string EmptiedAt = "SELECT emptied_at::text FROM content.item_text WHERE id = @id";
    private const string Events =
        "SELECT coalesce(string_agg(item_kind || ',' || item_id || ',' || step || ',' || reason || ',' || coalesce(actor_person_id::text, 'system') || ',' || coalesce(reference, '-'), "
        + "';' ORDER BY sequence), '') FROM public.custody_event WHERE step IN ('item_emptied', 'item_emptying_refused')";

    [DatabaseFact]
    public async Task ItemAndMapAreClearedColumnByColumnWithTheMarkerSet()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemWithMapAsync(world);

        var result = await EmptyAsync(world, curator, item);

        Assert.Equal(EmptyingOutcome.Emptied, result.Outcome);
        Assert.Equal(("true,true,true,true", "true,true"), (await world.OwnerInDomainAsync(1, ItemCleared, ("id", item)), await world.OwnerInDomainAsync(1, MapCleared, ("id", item))));
    }

    [DatabaseFact]
    public async Task EmptyingLeavesTheEventWithTheItemTheMotiveTheAuthorAndTheReference()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemWithMapAsync(world);

        var result = await EmptyAsync(world, curator, item, EmptyingMotive.ContractRequirement);

        Assert.Equal(($"item_text,{item},item_emptied,contract_requirement,{curator},{Protocol}", true), (await world.ScalarAsync(Events), result.Sequence is > 0));
    }

    [DatabaseFact]
    public async Task ChainStaysValidAfterTheEmptyingAndHoldsEventsWithAndWithoutReference()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemWithMapAsync(world);
        var read = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.Person(curator), ContentColumns.RawText, 1, item), TestContext.Current.CancellationToken);
        _ = await EmptyAsync(world, await world.PersonAsync("zqx-active"), item);
        _ = await EmptyAsync(world, curator, item);

        var verified = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.Read, 3L, null), (read.Outcome, verified.EventsRead, verified.FirstInconsistent?.Sequence));
    }

    [DatabaseFact]
    public async Task AuthorWithoutTheFunctionIsRefusedTheRefusalIsInTheChainAndTheContentIsIntact()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (_, item) = await ItemWithMapAsync(world);
        var annotator = await world.PersonAsync("zqx-active");

        var result = await EmptyAsync(world, annotator, item);

        Assert.Equal(EmptyingOutcome.AuthorWithoutFunction, result.Outcome);
        Assert.Equal(
            ($"item_text,{item},item_emptying_refused,author_without_function,{annotator},{Protocol}", Intact, "false,false"),
            (await world.ScalarAsync(Events), await world.OwnerInDomainAsync(1, ItemCleared, ("id", item)), await world.OwnerInDomainAsync(1, MapCleared, ("id", item))));
    }

    [DatabaseFact]
    public async Task TheSystemDoesNotHoldTheFunctionEitherAndIsRefusedAsTheSystem()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (_, item) = await ItemWithMapAsync(world);

        var result = await world.Emptier.EmptyAsync(new ItemEmptying(CustodyAuthor.System, 1, item, EmptyingMotive.ContractRequirement, Protocol), TestContext.Current.CancellationToken);

        Assert.Equal(EmptyingOutcome.AuthorWithoutFunction, result.Outcome);
        Assert.Equal($"item_text,{item},item_emptying_refused,author_without_function,system,{Protocol}", await world.ScalarAsync(Events));
    }

    [DatabaseFact]
    public async Task ItemEmptiedAlreadyIsRefusedTheRefusalIsInTheChainAndNothingIsEmptiedAgain()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemWithMapAsync(world);
        _ = await EmptyAsync(world, curator, item);
        var firstMoment = await world.OwnerInDomainAsync(1, EmptiedAt, ("id", item));

        var again = await EmptyAsync(world, curator, item);

        Assert.Equal(EmptyingOutcome.AlreadyEmptied, again.Outcome);
        Assert.Equal(
            (firstMoment, $"item_text,{item},item_emptied,removal_requested_by_subject,{curator},{Protocol};item_text,{item},item_emptying_refused,item_already_emptied,{curator},{Protocol}"),
            (await world.OwnerInDomainAsync(1, EmptiedAt, ("id", item)), await world.ScalarAsync(Events)));
    }

    [DatabaseFact]
    public async Task ItemOfAnotherDomainIsNotFoundAndNothingIsRecorded()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemWithMapAsync(world);

        var result = await world.Emptier.EmptyAsync(new ItemEmptying(CustodyAuthor.Person(curator), 2, item, EmptyingMotive.ContractRequirement, Protocol), TestContext.Current.CancellationToken);

        Assert.Equal((EmptyingOutcome.NotFound, string.Empty, Intact), (result.Outcome, await world.ScalarAsync(Events), await world.OwnerInDomainAsync(1, ItemCleared, ("id", item))));
    }

    [DatabaseTheory]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public async Task AttemptWithoutTheFunctionIsInTheChainWhateverTheItemIs(int domain, bool missing)
    {
        await using var world = await ContentWorld.CreateAsync();
        var (_, item) = await ItemWithMapAsync(world);
        var annotator = await world.PersonAsync("zqx-active");
        var target = missing ? item + 1000 : item;

        var result = await world.Emptier.EmptyAsync(
            new ItemEmptying(CustodyAuthor.Person(annotator), domain, target, EmptyingMotive.ContractRequirement, Protocol), TestContext.Current.CancellationToken);

        Assert.Equal(
            (EmptyingOutcome.AuthorWithoutFunction, $"item_text,{target},item_emptying_refused,author_without_function,{annotator},{Protocol}", Intact),
            (result.Outcome, await world.ScalarAsync(Events), await world.OwnerInDomainAsync(1, ItemCleared, ("id", item))));
    }

    [DatabaseTheory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ItemWithoutAPositiveIdentifierIsRefusedAtTheDoor(long item)
    {
        await using var world = await ContentWorld.CreateAsync();
        var curator = await world.PersonAsync("zqx-curator");

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => EmptyAsync(world, curator, item));
    }

    [DatabaseFact]
    public async Task ReferenceThatIsNotAProtocolNumberIsRefusedBeforeAnythingIsDone()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemWithMapAsync(world);

        var result = await world.Emptier.EmptyAsync(
            new ItemEmptying(CustodyAuthor.Person(curator), 1, item, EmptyingMotive.ContractRequirement, "Maria da Silva"), TestContext.Current.CancellationToken);

        Assert.Equal((EmptyingOutcome.ReferenceRefused, string.Empty, Intact), (result.Outcome, await world.ScalarAsync(Events), await world.OwnerInDomainAsync(1, ItemCleared, ("id", item))));
    }

    [DatabaseFact]
    public async Task DeactivatedAuthorWithoutTheFunctionIsNotRecordedAndNothingIsEmptied()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (_, item) = await ItemWithMapAsync(world);

        var result = await EmptyAsync(world, await world.PersonAsync("zqx-gone"), item);

        Assert.Equal((EmptyingOutcome.NotRecorded, CustodyOutcome.ActorDeactivated, string.Empty), (result.Outcome, result.Refusal?.Outcome, await world.ScalarAsync(Events)));
    }

    [DatabaseFact]
    public async Task CuratorWhoIsDeactivatedIsNotRecordedAndTheContentIsIntact()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemWithMapAsync(world);
        await world.Database.RunAsOwnerAsync("UPDATE public.person SET active = false WHERE name = 'zqx-curator'");

        var result = await EmptyAsync(world, curator, item);

        Assert.Equal((EmptyingOutcome.NotRecorded, CustodyOutcome.ActorDeactivated, Intact), (result.Outcome, result.Refusal?.Outcome, await world.OwnerInDomainAsync(1, ItemCleared, ("id", item))));
    }

    [DatabaseFact]
    public async Task EmptiedItemIsReadAsEmptiedForEveryColumnAndTheReadingIsNotRecorded()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemWithMapAsync(world);
        var map = (long)(await world.OwnerInDomainAsync(1, "SELECT id FROM content.reversal_map WHERE item_id = @id", ("id", item)))!;
        _ = await EmptyAsync(world, curator, item);
        var author = CustodyAuthor.Person(curator);

        var raw = await world.Reader.ReadAsync(new ContentRead(author, ContentColumns.RawText, 1, item), TestContext.Current.CancellationToken);
        var sanitized = await world.Reader.ReadAsync(new ContentRead(author, ContentColumns.SanitizedText, 1, item), TestContext.Current.CancellationToken);
        var entries = await world.Reader.ReadAsync(new ContentRead(author, ContentColumns.Entries, 1, map), TestContext.Current.CancellationToken);

        Assert.Equal(
            (ContentReadOutcome.Emptied, ContentReadOutcome.Emptied, ContentReadOutcome.Emptied, 0L),
            (raw.Outcome, sanitized.Outcome, entries.Outcome, await world.ScalarAsync("SELECT count(*) FROM public.custody_event WHERE step LIKE '%\\_read'")));
    }

    [DatabaseFact]
    public async Task ItemThatWasNeverEmptiedAndHoldsNoSanitizedTextIsReadAsEmptyAndNotAsEmptied()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto"), TestContext.Current.CancellationToken);

        var sanitized = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.SanitizedText, 1, item), TestContext.Current.CancellationToken);

        Assert.Equal(ContentReadOutcome.Empty, sanitized.Outcome);
    }

    [DatabaseFact]
    public async Task EmptyingIsNotKeptWhenTheChainDoesNotAcceptTheEvent()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemWithMapAsync(world);
        await world.BreakTheChainAsync();

        var result = await EmptyAsync(world, curator, item);

        Assert.Equal((EmptyingOutcome.NotRecorded, CustodyOutcome.ChainBroken, Intact), (result.Outcome, result.Refusal?.Outcome, await world.OwnerInDomainAsync(1, ItemCleared, ("id", item))));
    }

    [DatabaseFact]
    public async Task MapIsNotAddedToAnItemThatWasEmptied()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemWithMapAsync(world);
        _ = await EmptyAsync(world, curator, item);

        var refused = await Assert.ThrowsAsync<ItemEmptiedException>(() => world.Writer.AddReversalMapAsync(1, item, "outro mapa zqx", TestContext.Current.CancellationToken));

        var maps = (long)(await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.reversal_map WHERE item_id = @id", ("id", item)))!;
        Assert.Equal((1L, false), (maps, refused.Message.Contains("zqx", StringComparison.Ordinal)));
    }

    private static Task<EmptyingResult> EmptyAsync(ContentWorld world, long author, long item, EmptyingMotive motive = EmptyingMotive.RemovalRequestedBySubject) =>
        world.Emptier.EmptyAsync(new ItemEmptying(CustodyAuthor.Person(author), 1, item, motive, Protocol), TestContext.Current.CancellationToken);

    private static async Task<(long Curator, long Item)> ItemWithMapAsync(ContentWorld world)
    {
        var texts = new NewItemText("texto bruto zqx", "texto sanitizado zqx", "texto pseudonimizado zqx");
        var item = await world.Writer.AddItemTextAsync(1, texts, TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa de reversao zqx", TestContext.Current.CancellationToken);
        return (await world.PersonAsync("zqx-curator"), item);
    }
}
