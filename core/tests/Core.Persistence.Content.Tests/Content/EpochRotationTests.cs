using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>The rotation of the epoch wraps the keys of the live rows again, in one transaction, and touches neither the content nor what was emptied.</summary>
[Trait("Category", "Database")]
public sealed class EpochRotationTests
{
    private const string Raw = "texto bruto sintetico zqx";
    private const string Entries = "mapa de reversao zqx";
    private const string Epochs = "SELECT string_agg(dek_epoch::text, ',' ORDER BY id) FROM content.item_text";
    private const string MapEpochs = "SELECT string_agg(dek_epoch::text, ',' ORDER BY id) FROM content.reversal_map";

    [DatabaseFact]
    public async Task EveryLiveKeyOfEveryDomainIsWrappedAgainAndTheContentReadsAsBefore()
    {
        await using var world = await ContentWorld.CreateAsync();
        var first = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, "sanitizado"), TestContext.Current.CancellationToken);
        var map = await world.Writer.AddReversalMapAsync(1, first, Entries, TestContext.Current.CancellationToken);
        var second = await world.Writer.AddItemTextAsync(2, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var wrappedBefore = await world.StoredAsync(1, "SELECT dek_wrapped FROM content.item_text WHERE id = @id", first);
        using var epochs = SyntheticCustody.HoldingEpochs([1, 2], ContentWorld.Labels);

        var rewrapped = await new EpochRotation(world.DataSource, epochs.Keys).RotateAsync(TestContext.Current.CancellationToken);

        var wrappedAfter = await world.StoredAsync(1, "SELECT dek_wrapped FROM content.item_text WHERE id = @id", first);
        var reading = world.ReaderWith(epochs);
        var raw = await reading.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, first), TestContext.Current.CancellationToken);
        var entries = await reading.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.Entries, 1, map), TestContext.Current.CancellationToken);
        var other = await reading.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 2, second), TestContext.Current.CancellationToken);
        var epochsNow = (await world.InDomainAsync(1, Epochs), await world.InDomainAsync(1, MapEpochs), await world.InDomainAsync(2, Epochs));
        Assert.Equal((3, ("2", "2", "2"), false), (rewrapped, epochsNow, wrappedBefore.SequenceEqual(wrappedAfter)));
        Assert.Equal((Raw, Entries, Raw), (raw.Text, entries.Text, other.Text));
    }

    [DatabaseFact]
    public async Task RotatingAgainWithNothingOlderChangesNothing()
    {
        await using var world = await ContentWorld.CreateAsync();
        _ = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        using var epochs = SyntheticCustody.HoldingEpochs([1, 2], ContentWorld.Labels);
        var rotation = new EpochRotation(world.DataSource, epochs.Keys);

        var first = await rotation.RotateAsync(TestContext.Current.CancellationToken);
        var second = await rotation.RotateAsync(TestContext.Current.CancellationToken);

        Assert.Equal((1, 0), (first, second));
    }

    [DatabaseFact]
    public async Task RowsEmptiedBeforeTheRotationStayEmptied()
    {
        await using var world = await ContentWorld.CreateAsync();
        var gone = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var kept = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var curator = await world.PersonAsync("zqx-curator");
        _ = await world.Emptier.EmptyAsync(
            new ItemEmptying(CustodyAuthor.Person(curator), 1, gone, EmptyingMotive.ContractRequirement, "PROT-2026/0042"), TestContext.Current.CancellationToken);
        using var epochs = SyntheticCustody.HoldingEpochs([1, 2], ContentWorld.Labels);

        var rewrapped = await new EpochRotation(world.DataSource, epochs.Keys).RotateAsync(TestContext.Current.CancellationToken);

        Assert.Equal((1, "2"), (rewrapped, await world.InDomainAsync(1, "SELECT dek_epoch::text FROM content.item_text WHERE id = @id", ("id", kept))));
        Assert.Null(await world.InDomainAsync(1, "SELECT dek_epoch FROM content.item_text WHERE id = @id", ("id", gone)));
    }

    [DatabaseFact]
    public async Task RotationThatCannotUnwrapAKeyChangesNoRowAndNamesNothingOfTheKey()
    {
        await using var world = await ContentWorld.CreateAsync();
        _ = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddItemTextAsync(2, new NewItemText(Raw), TestContext.Current.CancellationToken);
        using var onlyTheNewOne = SyntheticCustody.HoldingEpochs([2], ContentWorld.Labels);

        var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(() => new EpochRotation(world.DataSource, onlyTheNewOne.Keys).RotateAsync(TestContext.Current.CancellationToken));

        Assert.Equal(("1", "1", true), (await world.InDomainAsync(1, Epochs) as string, await world.InDomainAsync(2, Epochs) as string, refused.Message.Contains("epoch 1", StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task WorldWithNoDomainRotatesNothing()
    {
        await using var world = await ContentWorld.CreateAsync();
        _ = await world.ScalarAsync("DELETE FROM public.domain");
        using var epochs = SyntheticCustody.HoldingEpochs([1, 2], ContentWorld.Labels);

        Assert.Equal(0, await new EpochRotation(world.DataSource, epochs.Keys).RotateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NullArgumentsAreRefusedByName()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels);

        Assert.Equal(
            ["dataSource", "keys"],
            [
                Assert.Throws<ArgumentNullException>(() => new EpochRotation(null!, custody.Keys)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new EpochRotation(dataSource, null!)).ParamName,
            ]);
    }
}
