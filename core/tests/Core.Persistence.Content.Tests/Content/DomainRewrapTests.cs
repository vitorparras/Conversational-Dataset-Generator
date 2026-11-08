using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Rotation;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The rewrap of a domain (issue 56, part 2): the key of each row not emptied is wrapped again under the newest generation registered and not revoked, in batches that each commit alone with an
/// event of the chain, never touching the content or the epoch; a run that stops goes on from where it was, and the status only reads.
/// </summary>
[Trait("Category", "Database")]
public sealed class DomainRewrapTests
{
    private const int Whole = 500;
    private const string Raw = "texto bruto sintetico zqx";
    private const string Entries = "mapa de reversao zqx";
    private const string Register = "INSERT INTO public.derivation_label (label) VALUES ('texto-bruto/1/v1'), ('mapa-reversao/1/v1'), ('texto-bruto/2/v1'), ('mapa-reversao/2/v1'), "
        + "('texto-bruto/1/v2'), ('mapa-reversao/1/v2')";
    private const string Events = "SELECT count(*) FROM public.custody_event WHERE step = 'domain_rewrapped' AND item_kind = 'domain' AND item_id = 1 AND actor_system";
    private const string AnyRewrapEvent = "SELECT count(*) FROM public.custody_event WHERE step = 'domain_rewrapped'";
    private const string ItemKey = "SELECT dek_wrapped FROM content.item_text WHERE id = @id";
    private const string MapKey = "SELECT dek_wrapped FROM content.reversal_map WHERE id = @id";
    private const string ItemRaw = "SELECT raw_text FROM content.item_text WHERE id = @id";
    private const string ItemEpoch = "SELECT dek_epoch::text FROM content.item_text WHERE id = @id";
    private static readonly string[] WithTheNextGeneration = [.. ContentWorld.Labels, "texto-bruto/1/v2", "mapa-reversao/1/v2"];

    [DatabaseFact]
    public async Task EveryKeyOfTheDomainPassesToTheNewestGenerationAndTheContentReadsAsBefore()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        var first = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, "sanitizado"), TestContext.Current.CancellationToken);
        var map = await world.Writer.AddReversalMapAsync(1, first, Entries, TestContext.Current.CancellationToken);
        var other = await world.Writer.AddItemTextAsync(2, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var sealedBefore = await world.StoredAsync(1, ItemRaw, first);
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);

        var rewrapped = await WholeBatches(world).RewrapAsync(1, custody.Keys, custody.Labels, TestContext.Current.CancellationToken);

        var reading = world.ReaderWith(custody);
        var raw = await reading.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, first), TestContext.Current.CancellationToken);
        var entries = await reading.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.Entries, 1, map), TestContext.Current.CancellationToken);
        Assert.Equal(
            (2, "texto-bruto/1/v2", "mapa-reversao/1/v2", "texto-bruto/2/v1", "1"),
            (rewrapped, await LabelOfAsync(world, 1, ItemKey, first), await LabelOfAsync(world, 1, MapKey, map), await LabelOfAsync(world, 2, ItemKey, other),
             await world.InDomainAsync(1, ItemEpoch, ("id", first)) as string));
        var sealedAfter = await world.StoredAsync(1, ItemRaw, first);
        Assert.Equal((Raw, Entries, true), (raw.Text, entries.Text, sealedBefore.SequenceEqual(sealedAfter)));
    }

    [DatabaseFact]
    public async Task EachBatchCommitsAloneWithAnEventOfItsOwnAndRunningAgainWritesNothing()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        _ = await AddItemsAsync(world.Writer, 5);
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);
        var rewrap = new DomainRewrap(world.DataSource, 2);

        var first = await rewrap.RewrapAsync(1, custody.Keys, custody.Labels, TestContext.Current.CancellationToken);
        var eventsAfterFirst = await world.ScalarAsync(Events);
        var second = await rewrap.RewrapAsync(1, custody.Keys, custody.Labels, TestContext.Current.CancellationToken);

        Assert.Equal((5, 3L, 0, 3L), (first, eventsAfterFirst, second, await world.ScalarAsync(Events)));
    }

    [DatabaseFact]
    public async Task RunThatStopsInTheMiddleKeepsTheBatchesCommittedAndTheNextOneGoesOnFromThere()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        var ids = await AddItemsAsync(world.Writer, 3);
        using var ahead = SyntheticCustody.HoldingEpochs([1, 2], ContentWorld.Labels);
        ids.Add(await new SeededWriter(new ContentWriter(world.DataSource, ahead.Content)).AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken));
        ids.AddRange(await AddItemsAsync(world.Writer, 1));
        using var onlyTheFirstEpoch = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);

        _ = await Assert.ThrowsAsync<EnvelopeRefusedException>(
            () => new DomainRewrap(world.DataSource, 2).RewrapAsync(1, onlyTheFirstEpoch.Keys, onlyTheFirstEpoch.Labels, TestContext.Current.CancellationToken));

        var stopped = (await LabelsOfAsync(world, ids), await world.ScalarAsync(Events));
        using var both = SyntheticCustody.HoldingEpochs([1, 2], WithTheNextGeneration);
        var status = await new DomainRewrap(world.DataSource, 2).StatusAsync(1, both.Labels, TestContext.Current.CancellationToken);
        var readAfterStop = await ReadAllAsync(world, both, ids);
        var rest = await new DomainRewrap(world.DataSource, 2).RewrapAsync(1, both.Keys, both.Labels, TestContext.Current.CancellationToken);

        Assert.Equal(("v2,v2,v1,v1,v1", 1L), (string.Join(',', stopped.Item1.Select(label => label[^2..])), stopped.Item2));
        Assert.Equal((new RewrapStatus(2, 3), 5), (status, readAfterStop.Count(text => text == Raw)));
        Assert.Equal((3, "v2,v2,v2,v2,v2", 3L), (rest, string.Join(',', (await LabelsOfAsync(world, ids)).Select(label => label[^2..])), await world.ScalarAsync(Events)));
        Assert.Equal(5, (await ReadAllAsync(world, both, ids)).Count(text => text == Raw));
    }

    [DatabaseFact]
    public async Task GenerationRevokedIsSkippedForTheNewestOneNotRevoked()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        var item = (await AddItemsAsync(world.Writer, 1))[0];
        _ = await world.ScalarAsync("INSERT INTO public.derivation_label (label) VALUES ('texto-bruto/1/v3'), ('mapa-reversao/1/v3')");
        _ = await world.ScalarAsync("INSERT INTO public.derivation_label_revocation (label) VALUES ('texto-bruto/1/v3'), ('mapa-reversao/1/v3')");
        using var custody = SyntheticCustody.HoldingEpochs([1], [.. WithTheNextGeneration, "texto-bruto/1/v3", "mapa-reversao/1/v3"]);

        _ = await WholeBatches(world).RewrapAsync(1, custody.Keys, custody.Labels, TestContext.Current.CancellationToken);

        Assert.Equal("texto-bruto/1/v2", await LabelOfAsync(world, 1, ItemKey, item));
    }

    [DatabaseFact]
    public async Task CustodyWithoutTheNewestGenerationRefusesNamingTheLabelAndWritesNothing()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        var ids = await AddItemsAsync(world.Writer, 2);
        using var custody = SyntheticCustody.HoldingEpochs([1], ContentWorld.Labels);
        var rewrap = WholeBatches(world);

        var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(() => rewrap.RewrapAsync(1, custody.Keys, custody.Labels, TestContext.Current.CancellationToken));
        var status = await Assert.ThrowsAsync<EnvelopeRefusedException>(() => rewrap.StatusAsync(1, custody.Labels, TestContext.Current.CancellationToken));

        Assert.Equal(
            (true, true, 0L, "v1,v1"),
            (refused.Message.Contains("texto-bruto/1/v2", StringComparison.Ordinal), status.Message.Contains("texto-bruto/1/v2", StringComparison.Ordinal),
             await world.ScalarAsync(Events), string.Join(',', (await LabelsOfAsync(world, ids)).Select(label => label[^2..]))));
    }

    [DatabaseFact]
    public async Task KeyWhoseHeaderNamesALabelOfAnotherDomainStopsTheRun()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        var other = await world.Writer.AddItemTextAsync(2, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var foreign = await world.StoredAsync(2, ItemKey, other);
        _ = await world.InDomainAsync(1, "INSERT INTO content.item_text (domain_id, batch_id, raw_text, dek_epoch, dek_wrapped) VALUES (1, 1, '\x01', 1, @wrapped) RETURNING id", ("wrapped", foreign));
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);

        var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(() => WholeBatches(world).RewrapAsync(1, custody.Keys, custody.Labels, TestContext.Current.CancellationToken));

        Assert.Equal((true, 0L), (refused.Message.Contains("label in the header of a key", StringComparison.Ordinal), await world.ScalarAsync(Events)));
    }

    [DatabaseFact]
    public async Task ChainThatDoesNotAcceptTheEventStopsTheRunAndChangesNoKey()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        var ids = await AddItemsAsync(world.Writer, 2);
        await world.BreakTheChainAsync();
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => WholeBatches(world).RewrapAsync(1, custody.Keys, custody.Labels, TestContext.Current.CancellationToken));

        Assert.Equal((true, "v1,v1"), (refused.Message.Contains("ChainBroken", StringComparison.Ordinal), string.Join(',', (await LabelsOfAsync(world, ids)).Select(label => label[^2..]))));
    }

    [DatabaseFact]
    public async Task RowsEmptiedBeforeTheRewrapStayEmptiedAndAreNotCounted()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        var ids = await AddItemsAsync(world.Writer, 2);
        var curator = await world.PersonAsync("zqx-curator");
        _ = await world.Emptier.EmptyAsync(
            new ItemEmptying(CustodyAuthor.Person(curator), 1, ids[0], EmptyingMotive.ContractRequirement, "PROT-2026/0042"), TestContext.Current.CancellationToken);
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);

        var rewrapped = await WholeBatches(world).RewrapAsync(1, custody.Keys, custody.Labels, TestContext.Current.CancellationToken);

        Assert.Equal((1, "texto-bruto/1/v2", null), (rewrapped, await LabelOfAsync(world, 1, ItemKey, ids[1]), await world.InDomainAsync(1, ItemEpoch, ("id", ids[0]))));
    }

    [DatabaseFact]
    public async Task StatusCountsWhatIsAndWhatIsNotOnTheNewestGenerationAndWritesNothing()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        _ = await AddItemsAsync(world.Writer, 3);
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);
        _ = await new SeededWriter(new ContentWriter(world.DataSource, custody.Content)).AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var rewrap = new DomainRewrap(world.DataSource, 2);

        var before = await rewrap.StatusAsync(1, custody.Labels, TestContext.Current.CancellationToken);
        var eventsAfterStatus = await world.ScalarAsync(Events);
        _ = await rewrap.RewrapAsync(1, custody.Keys, custody.Labels, TestContext.Current.CancellationToken);
        var after = await rewrap.StatusAsync(1, custody.Labels, TestContext.Current.CancellationToken);

        Assert.Equal((new RewrapStatus(1, 3), 0L, new RewrapStatus(4, 0)), (before, eventsAfterStatus, after));
    }

    [DatabaseFact]
    public async Task DomainWithNoRowsIsRewrappedAndCountedAsNothingAndWritesNoEvent()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);
        var rewrap = WholeBatches(world);

        var rewrapped = await rewrap.RewrapAsync(2, custody.Keys, custody.Labels, TestContext.Current.CancellationToken);

        Assert.Equal((0, new RewrapStatus(0, 0), 0L), (rewrapped, await rewrap.StatusAsync(2, custody.Labels, TestContext.Current.CancellationToken), await world.ScalarAsync(Events)));
    }

    [DatabaseFact]
    public async Task RewrappingADomainThatDoesNotExistIsRefusedNamingItAndWritesNoEvent()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => WholeBatches(world).RewrapAsync(99, custody.Keys, custody.Labels, TestContext.Current.CancellationToken));

        Assert.Equal(("The domain 99 does not exist.", 0L), (refused.Message, await world.ScalarAsync(AnyRewrapEvent)));
    }

    [DatabaseFact]
    public async Task TheStatusOfADomainThatDoesNotExistIsRefusedNamingItAndWritesNoEvent()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => WholeBatches(world).StatusAsync(99, custody.Labels, TestContext.Current.CancellationToken));

        Assert.Equal(("The domain 99 does not exist.", 0L), (refused.Message, await world.ScalarAsync(AnyRewrapEvent)));
    }

    [DatabaseFact]
    public async Task StatusSumsTheItemsAndTheMapsApartInEachCountByTheirOwnGeneration()
    {
        await using var world = await WorldWithTheNextGenerationAsync();
        _ = await AddItemsAsync(world.Writer, 1);
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);
        var newest = new SeededWriter(new ContentWriter(world.DataSource, custody.Content));
        var current = new List<long>();
        for (var count = 0; count < 3; count++)
        {
            current.Add(await newest.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken));
        }

        _ = await newest.AddReversalMapAsync(1, current[2], Entries, TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, current[0], Entries, TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, current[1], Entries, TestContext.Current.CancellationToken);

        var status = await WholeBatches(world).StatusAsync(1, custody.Labels, TestContext.Current.CancellationToken);

        Assert.Equal((4, 3), (status.Current, status.Behind));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TheStatusOfADomainThatIsNotPositiveIsRefusedByTheDomainParameterBeforeAnyConnectionOpens(int domainId)
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels);

        var refused = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new DomainRewrap(dataSource, Whole).StatusAsync(domainId, custody.Labels, TestContext.Current.CancellationToken));

        Assert.Equal("domainId", refused.ParamName);
    }

    [Fact]
    public async Task ArgumentsAreRefusedByNameBeforeAnyConnectionOpens()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels);
        var rewrap = new DomainRewrap(dataSource, Whole);
        var token = TestContext.Current.CancellationToken;

        var names = new[]
        {
            Assert.Throws<ArgumentNullException>(() => new DomainRewrap(null!, Whole)).ParamName,
            Assert.Throws<ArgumentOutOfRangeException>(() => new DomainRewrap(dataSource, 0)).ParamName,
            (await Assert.ThrowsAsync<ArgumentNullException>(() => rewrap.RewrapAsync(1, null!, custody.Labels, token))).ParamName,
            (await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => rewrap.RewrapAsync(0, custody.Keys, custody.Labels, token))).ParamName,
            (await Assert.ThrowsAsync<ArgumentNullException>(() => rewrap.RewrapAsync(1, custody.Keys, null!, token))).ParamName,
            (await Assert.ThrowsAsync<ArgumentNullException>(() => rewrap.StatusAsync(1, null!, token))).ParamName,
        };

        Assert.Equal(["dataSource", "batch", "keys", "domainId", "loaded", "loaded"], names);
    }

    private static async Task<ContentWorld> WorldWithTheNextGenerationAsync()
    {
        var world = await ContentWorld.CreateAsync();
        _ = await world.ScalarAsync(Register);
        return world;
    }

    private static async Task<List<long>> AddItemsAsync(SeededWriter writer, int count)
    {
        var ids = new List<long>();
        for (var index = 0; index < count; index++)
        {
            ids.Add(await writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken));
        }

        return ids;
    }

    private static DomainRewrap WholeBatches(ContentWorld world) => new(world.DataSource, Whole);

    private static async Task<string> LabelOfAsync(ContentWorld world, int domainId, string statement, long id) =>
        EnvelopeCipher.LabelOf(await world.StoredAsync(domainId, statement, id));

    private static async Task<List<string>> ReadAllAsync(ContentWorld world, SyntheticCustody custody, List<long> ids)
    {
        var texts = new List<string>();
        foreach (var id in ids)
        {
            var read = await world.ReaderWith(custody).ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, id), TestContext.Current.CancellationToken);
            Assert.NotNull(read.Text);
            texts.Add(read.Text);
        }

        return texts;
    }

    private static async Task<List<string>> LabelsOfAsync(ContentWorld world, List<long> ids)
    {
        var labels = new List<string>();
        foreach (var id in ids)
        {
            labels.Add(await LabelOfAsync(world, 1, ItemKey, id));
        }

        return labels;
    }
}
