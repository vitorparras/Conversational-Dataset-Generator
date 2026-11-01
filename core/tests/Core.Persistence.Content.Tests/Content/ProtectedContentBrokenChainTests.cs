using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// While the last verification of the chain failed (#61, CA-5), content is not written through the one way that writes it, the refusal is visible and nothing is left of it, reading what needs
/// no event goes on, reading protected content is not opened because the chain cannot record it, and a verification that passes again lifts the refusal.
/// </summary>
[Trait("Category", "Database")]
public sealed class ProtectedContentBrokenChainTests
{
    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";
    private const string CountItems = "SELECT count(*) FROM content.item_text";
    private const string CountMaps = "SELECT count(*) FROM content.reversal_map";
    private const string Raw = "texto bruto sintetico zqx";
    private const string Pseudonymized = "texto pseudonimizado sintetico zqx";

    [DatabaseFact]
    public async Task ItemAndMapAreRefusedVisiblyAndNothingIsLeftOfThem()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        await BreakAsync(world);

        var refusedItem = await Assert.ThrowsAsync<CustodyChainBrokenException>(
            () => world.Writer.AddItemTextAsync(1, new NewItemText("outro texto sintetico zqx"), TestContext.Current.CancellationToken));
        var refusedMap = await Assert.ThrowsAsync<CustodyChainBrokenException>(
            () => world.Writer.AddReversalMapAsync(1, item, "mapa sintetico zqx", TestContext.Current.CancellationToken));

        Assert.DoesNotContain("zqx", refusedItem.Message + refusedMap.Message, StringComparison.Ordinal);
        Assert.Equal((1L, 0L), (await CountAsync(world, CountItems), await CountAsync(world, CountMaps)));
    }

    [DatabaseFact]
    public async Task ContentThatNeedsNoEventIsStillReadAndSoIsTheChain()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, null, Pseudonymized), TestContext.Current.CancellationToken);
        await BreakAsync(world);

        var text = await world.InDomainAsync(1, "SELECT pseudonymized_text FROM content.item_text WHERE id = @id", ("id", item));

        Assert.Equal(Pseudonymized, text);
        Assert.Equal(3L, await world.ScalarAsync("SELECT count(*) FROM public.custody_event"));
    }

    [DatabaseFact]
    public async Task ProtectedContentIsNotOpenedBecauseTheReadingCannotBeRecordedAndTheRefusalIsTheChains()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        await BreakAsync(world);

        var result = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, item), TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.NotRecorded, null, CustodyOutcome.ChainBroken), (result.Outcome, result.Text, result.Refusal?.Outcome));
    }

    [DatabaseFact]
    public async Task VerificationThatPassesAgainLetsContentBeWrittenAndReadAgain()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        await BreakAsync(world);
        await RunAsSuperuserAsync(world, $"{Untrusted}UPDATE public.custody_event SET item_id = item_id - 1 WHERE sequence = 1{Trusted}");

        var passed = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);
        var added = await world.Writer.AddItemTextAsync(1, new NewItemText("outro texto sintetico zqx"), TestContext.Current.CancellationToken);
        var read = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, item), TestContext.Current.CancellationToken);

        Assert.True(passed.Passed);
        Assert.True(added > item);
        Assert.Equal((ContentReadOutcome.Read, Raw), (read.Outcome, read.Text));
    }

    /// <summary>Two events, one changed outside the system, and a verification that failed and was recorded.</summary>
    private static async Task BreakAsync(ContentWorld world)
    {
        var chain = new CustodyChain(world.DataSource);
        foreach (var item in new long[] { 1, 2 })
        {
            var added = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
            Assert.Equal(CustodyOutcome.Appended, (await chain.AppendAsync(added, TestContext.Current.CancellationToken)).Outcome);
        }

        await RunAsSuperuserAsync(world, $"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 1{Trusted}");
        var failed = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, failed.FirstInconsistent?.Sequence);
    }

    private static async Task<long> CountAsync(ContentWorld world, string statement) => (long)(await world.OwnerInDomainAsync(1, statement))!;

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the calling tests, never input.")]
    private static async Task RunAsSuperuserAsync(ContentWorld world, string statement)
    {
        var name = new NpgsqlConnectionStringBuilder(world.Database.AppConnectionString).Database;
        var connectionString = new NpgsqlConnectionStringBuilder((await PostgresFixture.SharedAsync()).SuperuserConnectionString) { Database = name, Pooling = false }.ConnectionString;
        await using var connection = await Sql.OpenAsync(connectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
