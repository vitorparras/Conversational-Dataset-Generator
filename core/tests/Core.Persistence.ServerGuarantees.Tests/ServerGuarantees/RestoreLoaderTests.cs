using System.Text.Json.Nodes;
using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.ServerGuarantees.Restore;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The load of a copy by the application alone (#64, CA-5): a database made by the flows it has comes back in a new one with the sequence and the digests of every event, the chain and the guarantees hold,
/// the restore is an event with the copy and the tip before and after, and a copy that is out of order, altered or short is refused with the divergence named and no event of restore.
/// </summary>
[Trait("Category", "Database")]
public sealed class RestoreLoaderTests
{
    private const string Digests = "SELECT string_agg(sequence || ':' || encode(previous_digest, 'hex') || ':' || encode(digest, 'hex'), ',' ORDER BY sequence) FROM public.custody_event WHERE sequence <= {0}";
    private const string Window = "SELECT coalesce(string_agg(state || ':' || coalesce(approved::text, '-'), ',' ORDER BY state), 'none') FROM public.restore_window";
    private const string Restored = "SELECT count(*)::text FROM public.custody_event WHERE step = 'restored'";

    [DatabaseFact]
    public async Task PopulatedDatabaseComesBackWithTheApplicationAloneEveryEventKeepsItsDigestsAndTheChainAndGuaranteesHold()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = await PopulatedAsync();
        using var copy = await CopyFolder.OfAsync(source);
        var (sequence, digest) = await CopyFolder.TipOfAsync(source);
        await using var destination = await FidelityWorld.CreateAsync(seeded: false);

        var result = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        Assert.Equal((RestoreOutcome.Restored, string.Empty), (result.Outcome, result.Detail));
        Assert.Equal(await source.OwnerColumnAsync(DigestsUpTo(sequence)), await destination.OwnerColumnAsync(DigestsUpTo(sequence)));
        Assert.True((await new CustodyVerifier(destination.DataSource, TimeProvider.System).VerifyAsync(token)).Passed);
        Assert.Equal(GuaranteeKinds.None, (await destination.Verifier().VerifyAsync(token)).Missing);
        Assert.Equal(["closed:true,opened:-"], await destination.OwnerColumnAsync(Window));
    }

    [DatabaseFact]
    public async Task RestoreIsAnEventAfterTheVerificationsNamingTheCopyAndTheTipBeforeAndAfter()
    {
        await using var source = await PopulatedAsync();
        using var copy = await CopyFolder.OfAsync(source);
        var (sequence, digest) = await CopyFolder.TipOfAsync(source);
        await using var destination = await FidelityWorld.CreateAsync(seeded: false);

        _ = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        var name = RestoreCopy.Open(copy.Path).Name;
        Assert.Equal(["verify_chain,verify_guarantees,restored"], await destination.OwnerColumnAsync($"SELECT string_agg(step, ',' ORDER BY sequence) FROM public.custody_event WHERE sequence > {sequence}"));
        Assert.Equal(
            [$"{copy.RowCount}|verification_passed|true|copy={name};before={sequence};after={sequence + 2}"],
            await destination.OwnerColumnAsync("SELECT item_id || '|' || reason || '|' || actor_system || '|' || reference FROM public.custody_event WHERE step = 'restored'"));
    }

    [DatabaseTheory]
    [InlineData("swapped", 2)]
    [InlineData("digest", 3)]
    public async Task EventOutOfOrderOrWithAnotherDigestIsRefusedNamingTheEventWithNoRestoreRecorded(string damage, int named)
    {
        await using var source = await PopulatedAsync();
        using var copy = await CopyFolder.OfAsync(source);
        var (sequence, digest) = await CopyFolder.TipOfAsync(source);
        copy.Rewrite("public.custody_event", events => damage == "swapped" ? [events[1], events[0], .. events.Skip(2)] : Altered(events, named - 1));
        await using var destination = await FidelityWorld.CreateAsync(seeded: false);

        var result = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        Assert.Equal(RestoreOutcome.RowsRefused, result.Outcome);
        Assert.Contains($"evento {named} ", result.Detail, StringComparison.Ordinal);
        await AssertNothingRestoredAsync(destination);
    }

    [DatabaseFact]
    public async Task CopyWithoutTheLastEventIsRefusedNamingBothTipsWithNoRestoreRecorded()
    {
        await using var source = await PopulatedAsync();
        using var copy = await CopyFolder.OfAsync(source);
        var (sequence, digest) = await CopyFolder.TipOfAsync(source);
        copy.Rewrite("public.custody_event", events => events.Take(events.Count - 1));
        await using var destination = await FidelityWorld.CreateAsync(seeded: false);

        var result = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        Assert.Equal(RestoreOutcome.TipDiffers, result.Outcome);
        Assert.Contains($"{sequence}:{digest}", result.Detail, StringComparison.Ordinal);
        Assert.Contains($"{sequence - 1}:", result.Detail, StringComparison.Ordinal);
        await AssertNothingRestoredAsync(destination);
    }

    [DatabaseFact]
    public async Task TipWithAnotherDigestIsRefusedEvenWhenTheSequenceIsTheSame()
    {
        await using var source = await PopulatedAsync();
        using var copy = await CopyFolder.OfAsync(source);
        var (sequence, _) = await CopyFolder.TipOfAsync(source);
        await using var destination = await FidelityWorld.CreateAsync(seeded: false);

        var result = await LoadAsync(destination, copy, new RestoreTip(sequence, new string('0', 64)));

        Assert.Equal(RestoreOutcome.TipDiffers, result.Outcome);
        await AssertNothingRestoredAsync(destination);
    }

    [DatabaseFact]
    public async Task CopyOfAnotherVersionOfTheSchemaOrWithoutATableIsRefusedBeforeAnythingIsPutBack()
    {
        await using var source = await PopulatedAsync();
        using var copy = await CopyFolder.OfAsync(source);
        var (sequence, digest) = await CopyFolder.TipOfAsync(source);
        await using var destination = await FidelityWorld.CreateAsync(seeded: false);
        var version = int.Parse(Assert.Single(await destination.OwnerColumnAsync("SELECT max(version)::text FROM public.schema_version")), System.Globalization.CultureInfo.InvariantCulture);

        copy.WriteManifest(version + 1, CopyFolder.Tables);
        var other = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));
        copy.WriteManifest(version, CopyFolder.Tables.Skip(1));
        var lacking = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        Assert.Equal((RestoreOutcome.CopyRefused, RestoreOutcome.CopyRefused), (other.Outcome, lacking.Outcome));
        Assert.Contains($"versão {version + 1}", other.Detail, StringComparison.Ordinal);
        Assert.Contains("8 das 9 tabelas", lacking.Detail, StringComparison.Ordinal);
        Assert.Equal(["none"], await destination.OwnerColumnAsync(Window));
    }

    [DatabaseFact]
    public async Task SecondLoadIntoTheSameDatabaseIsRefusedBecauseTheWindowOpensOnce()
    {
        await using var source = await PopulatedAsync();
        using var copy = await CopyFolder.OfAsync(source);
        var (sequence, digest) = await CopyFolder.TipOfAsync(source);
        await using var destination = await FidelityWorld.CreateAsync(seeded: false);
        _ = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        var result = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        Assert.Equal(RestoreOutcome.NotEmpty, result.Outcome);
        Assert.Equal(["1"], await destination.OwnerColumnAsync(Restored));
    }

    [DatabaseFact]
    public async Task GuaranteeThatIsMissingAfterTheLoadIsNamedAndTheRestoreIsRecordedAsFailed()
    {
        await using var source = await PopulatedAsync();
        using var copy = await CopyFolder.OfAsync(source);
        var (sequence, digest) = await CopyFolder.TipOfAsync(source);
        await using var destination = await FidelityWorld.CreateAsync(seeded: false);
        await destination.RunAsSuperuserAsync("ALTER TABLE content.item_text NO FORCE ROW LEVEL SECURITY");

        var result = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        Assert.Equal(RestoreOutcome.GuaranteesMissing, result.Outcome);
        Assert.Contains(nameof(GuaranteeKinds.RowPolicy), result.Detail, StringComparison.Ordinal);
        Assert.Equal(["verification_failed"], await destination.OwnerColumnAsync("SELECT reason FROM public.custody_event WHERE step = 'restored'"));
    }

    private static string DigestsUpTo(long sequence) => string.Format(System.Globalization.CultureInfo.InvariantCulture, Digests, sequence);

    private static Task<RestoreResult> LoadAsync(FidelityWorld destination, CopyFolder copy, RestoreTip tip) =>
        new RestoreLoader(destination.DataSource, destination.Verifier(), TimeProvider.System).LoadAsync(RestoreCopy.Open(copy.Path), tip, TestContext.Current.CancellationToken);

    private static IEnumerable<JsonNode> Altered(List<JsonNode> events, int index)
    {
        var digest = (string)events[index]["digest"]!;
        events[index]["digest"] = digest[..^1] + (digest[^1] == '0' ? '1' : '0');
        return events;
    }

    private static async Task AssertNothingRestoredAsync(FidelityWorld destination)
    {
        Assert.Equal(["closed:false,opened:-"], await destination.OwnerColumnAsync(Window));
        Assert.Equal(["0"], await destination.OwnerColumnAsync(Restored));
    }

    private static async Task<FidelityWorld> PopulatedAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var world = await FidelityWorld.CreateAsync();
        await world.MakeCuratorAsync();
        var first = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx", "texto sanitizado sintetico zqx"), token);
        _ = await world.Writer.AddReversalMapAsync(1, first, "mapa sintetico zqx", token);
        var second = await world.Writer.AddItemTextAsync(1, new NewItemText("segundo texto sintetico zqx"), token);
        _ = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, first), token);
        var person = (long)(await world.InDomainAsync(1, "SELECT id FROM public.person WHERE name = 'zqx-active'"))!;
        _ = await new ContentEmptier(world.DataSource).EmptyAsync(new ItemEmptying(CustodyAuthor.Person(person), 1, second, EmptyingMotive.ContractRequirement, "PROT-2026/0042"), token);
        _ = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(token);
        _ = await world.Verifier().VerifyAsync(token);
        return world;
    }
}
