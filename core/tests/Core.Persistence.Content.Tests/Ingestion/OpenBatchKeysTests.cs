using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Rotation;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The rotation of the epoch and the rewrap of a domain reach the items and the maps of a batch that is still open, because they only wrap a key again: a batch accepted afterwards must read with
/// its keys on the epoch and on the generation that are held, and the reading of the stock still does not see the batch while it is open.
/// </summary>
[Trait("Category", "Database")]
public sealed class OpenBatchKeysTests
{
    private const string Raw = "texto bruto sintetico zqx";
    private const string Entries = "mapa sintetico zqx";
    private const string ItemEpoch = "SELECT dek_epoch::text FROM content.item_text WHERE id = @id";
    private const string MapEpoch = "SELECT dek_epoch::text FROM content.reversal_map WHERE id = @id";
    private const string ItemKey = "SELECT dek_wrapped FROM content.item_text WHERE id = @id";
    private const string MapKey = "SELECT dek_wrapped FROM content.reversal_map WHERE id = @id";
    private const string Register = "INSERT INTO public.derivation_label (label) VALUES ('texto-bruto/1/v1'), ('mapa-reversao/1/v1'), ('texto-bruto/2/v1'), ('mapa-reversao/2/v1'), "
        + "('texto-bruto/1/v2'), ('mapa-reversao/1/v2')";

    private static readonly string[] WithTheNextGeneration = [.. ContentWorld.Labels, "texto-bruto/1/v2", "mapa-reversao/1/v2"];

    [DatabaseFact]
    public async Task OpenBatchIsRotatedToTheNewEpochAndReadsOnceAccepted()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        var map = await IngestionWorld.AddMapAsync(world, batch, item);
        using var epochs = SyntheticCustody.HoldingEpochs([1, 2], ContentWorld.Labels);

        var rewrapped = await new EpochRotation(world.DataSource, epochs.Keys).RotateAsync(TestContext.Current.CancellationToken);
        var hiddenWhileOpen = await world.InDomainAsync(IngestionWorld.Domain, IngestionWorld.Reachable) as string;
        _ = await IngestionWorld.AcceptAsync(world, batch);

        var reading = world.ReaderWith(epochs);
        var raw = await reading.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, IngestionWorld.Domain, item), TestContext.Current.CancellationToken);
        var entries = await reading.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.Entries, IngestionWorld.Domain, map), TestContext.Current.CancellationToken);
        Assert.Equal((2, "0,0"), (rewrapped, hiddenWhileOpen));
        Assert.Equal(
            ("2", "2", Raw, Entries),
            (await EpochOfAsync(world, ItemEpoch, item), await EpochOfAsync(world, MapEpoch, map), raw.Text, entries.Text));
    }

    [DatabaseFact]
    public async Task OpenBatchIsRewrappedToTheNewestGenerationAndReadsOnceAccepted()
    {
        await using var world = await IngestionWorld.CreateAsync();
        _ = await world.ScalarAsync(Register);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        var map = await IngestionWorld.AddMapAsync(world, batch, item);
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);

        var rewrapped = await new DomainRewrap(world.DataSource, 500).RewrapAsync(IngestionWorld.Domain, custody.Keys, custody.Labels, TestContext.Current.CancellationToken);
        var hiddenWhileOpen = await world.InDomainAsync(IngestionWorld.Domain, IngestionWorld.Reachable) as string;
        _ = await IngestionWorld.AcceptAsync(world, batch);

        var reading = world.ReaderWith(custody);
        var raw = await reading.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, IngestionWorld.Domain, item), TestContext.Current.CancellationToken);
        var entries = await reading.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.Entries, IngestionWorld.Domain, map), TestContext.Current.CancellationToken);
        Assert.Equal((2, "0,0"), (rewrapped, hiddenWhileOpen));
        Assert.Equal(
            ("texto-bruto/1/v2", "mapa-reversao/1/v2", Raw, Entries),
            (await LabelOfAsync(world, ItemKey, item), await LabelOfAsync(world, MapKey, map), raw.Text, entries.Text));
    }

    [DatabaseFact]
    public async Task StatusOfTheRewrapCountsTheKeysOfAnOpenBatchToo()
    {
        await using var world = await IngestionWorld.CreateAsync();
        _ = await world.ScalarAsync(Register);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AddMapAsync(world, batch, item);
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);

        var status = await new DomainRewrap(world.DataSource, 500).StatusAsync(IngestionWorld.Domain, custody.Labels, TestContext.Current.CancellationToken);

        Assert.Equal((0, 2), (status.Current, status.Behind));
    }

    [DatabaseFact]
    public async Task StockOfTheDomainIsNotOpenedByTheScopeOfTheSystemWhenNoOperationDeclaredIt()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, batch);

        var withScope = await InScopeOfTheSystemAsync(world, IngestionWorld.Reachable);

        Assert.Equal(("0,0", "1,0"), (await world.InDomainAsync(IngestionWorld.Domain, IngestionWorld.Reachable) as string, withScope));
    }

    [DatabaseTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScopeOfTheSystemEndsWithItsTransactionAndTheNextReadOfTheStockOnTheSameConnectionDoesNotSeeTheOpenBatch(bool commit)
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        var pooled = new NpgsqlConnectionStringBuilder(world.Database.AppConnectionString) { Pooling = true, MinPoolSize = 0, MaxPoolSize = 1 }.ConnectionString;
        await using var source = NpgsqlDataSource.Create(pooled);

        var inside = await ReadAsync(source, system: true, commit);
        var next = await ReadAsync(source, system: false, commit: true);

        Assert.Equal((inside.Connection, "1,0", "system"), (next.Connection, inside.Reachable, inside.Scope));
        Assert.Equal(("0,0", true), (next.Reachable, next.Scope != "system"));
    }

    private static async Task<(string Connection, string Reachable, string Scope)> ReadAsync(NpgsqlDataSource source, bool system, bool commit)
    {
        var transaction = system
            ? await ContentTransaction.BeginSystemAsync(source, IngestionWorld.Domain, TestContext.Current.CancellationToken)
            : await ContentTransaction.BeginAsync(source, IngestionWorld.Domain, TestContext.Current.CancellationToken);
        await using (transaction)
        {
            var seen = (await ScalarAsync(transaction, "SELECT pg_catalog.pg_backend_pid()::text"), await ScalarAsync(transaction, IngestionWorld.Reachable),
                await ScalarAsync(transaction, "SELECT coalesce(pg_catalog.current_setting('app.content_scope', true), 'unset')"));
            if (commit)
            {
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            return seen;
        }
    }

    private static async Task<string> ScalarAsync(ContentTransaction transaction, string statement)
    {
        await using var command = transaction.CreateCommand(statement);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<string?> InScopeOfTheSystemAsync(ContentWorld world, string statement)
    {
        await using var transaction = await ContentTransaction.BeginSystemAsync(world.DataSource, IngestionWorld.Domain, TestContext.Current.CancellationToken);
        await using var command = transaction.CreateCommand(statement);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) as string;
    }

    private static async Task<string?> EpochOfAsync(ContentWorld world, string statement, long id) =>
        await world.InDomainAsync(IngestionWorld.Domain, statement, ("id", id)) as string;

    private static async Task<string> LabelOfAsync(ContentWorld world, string statement, long id) =>
        EnvelopeCipher.LabelOf(await world.StoredAsync(IngestionWorld.Domain, statement, id));
}
