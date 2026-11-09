using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.People;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.ServerGuarantees.Restore;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The load of a copy by the application alone (#64, CA-5): a database made by the flows it has comes back in a new one with the sequence and
/// the digests of every event, the chain and the guarantees hold, the restore is an event with the copy and the tip before and after,
/// and a copy that is out of order, altered or short is refused with the divergence named and no event of restore.
/// </summary>
[Trait("Category", "Database")]
public sealed class RestoreLoaderTests
{
    private const string People = "SELECT string_agg(id || ':' || name || ':' || function_name || ':' || created_at || ':' || active, ',' ORDER BY id) FROM public.person";
    private const string Credentials =
        "SELECT string_agg(person_id || ':' || algorithm || ':' || work_factor || ':' || encode(salt, 'hex')"
        + " || ':' || encode(derived, 'hex'), ',' ORDER BY person_id) FROM public.person_credential";
    private const string Attempts = "SELECT string_agg(id || ':' || occurred_at || ':' || coalesce(network::text, '-'), ',' ORDER BY id) FROM public.sign_in_attempt";
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
        Assert.Equal(await source.OwnerColumnAsync(People), await destination.OwnerColumnAsync(People));
        Assert.Equal(await source.OwnerColumnAsync(Credentials), await destination.OwnerColumnAsync(Credentials));
        Assert.Equal(await source.OwnerColumnAsync(Attempts), await destination.OwnerColumnAsync(Attempts));
        Assert.Equal(["203.0.113.0/24"], await destination.OwnerColumnAsync("SELECT network::text FROM public.sign_in_attempt"));
        Assert.Equal(["zqx-flow:false"], await destination.OwnerColumnAsync("SELECT name || ':' || active FROM public.person WHERE name = 'zqx-flow'"));
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
        Assert.Equal(
            ["verify_chain,verify_guarantees,restored"],
            await destination.OwnerColumnAsync(
                $"SELECT string_agg(step, ',' ORDER BY sequence) FROM public.custody_event WHERE sequence > {sequence}"));
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
        copy.Rewrite("public.custody_event", events => damage == "swapped" ? [events[1], events[0], .. events.Skip(2)] : [.. events.Select((each, at) => at == named - 1 ? Spoiled(each) : each)]);
        await using var destination = await FidelityWorld.CreateAsync(seeded: false);

        var result = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        Assert.Equal(RestoreOutcome.RowsRefused, result.Outcome);
        Assert.Contains($"evento {named} ", result.Detail, StringComparison.Ordinal);
        await AssertNothingRestoredAsync(destination);
    }

    [DatabaseFact]
    public async Task LineThatIsNotJsonClosesTheWindowAsFailedNamingTheTable()
    {
        await using var source = await PopulatedAsync();
        using var copy = await CopyFolder.OfAsync(source);
        var (sequence, digest) = await CopyFolder.TipOfAsync(source);
        await File.AppendAllTextAsync(Path.Combine(copy.Path, "public.person.jsonl"), "{not json" + Environment.NewLine, TestContext.Current.CancellationToken);
        await using var destination = await FidelityWorld.CreateAsync(seeded: false);

        var result = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        Assert.Equal(RestoreOutcome.CopyRefused, result.Outcome);
        Assert.Contains("public.person ", result.Detail, StringComparison.Ordinal);
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
        var version = int.Parse(Assert.Single(await destination.OwnerColumnAsync("SELECT max(version)::text FROM public.schema_version")), CultureInfo.InvariantCulture);

        copy.WriteManifest(version + 1, CopyFolder.Tables);
        var other = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));
        copy.WriteManifest(version, CopyFolder.Tables.Skip(1));
        var lacking = await LoadAsync(destination, copy, new RestoreTip(sequence, digest));

        Assert.Equal((RestoreOutcome.CopyRefused, RestoreOutcome.CopyRefused), (other.Outcome, lacking.Outcome));
        Assert.Contains($"versão {version + 1}", other.Detail, StringComparison.Ordinal);
        Assert.Contains("14 das 15 tabelas", lacking.Detail, StringComparison.Ordinal);
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

    // nosemgrep: sql-concatenation-csharp interpolates only a sequence the test read from the database, a number, never input.
    private static string DigestsUpTo(long sequence) =>
        $"SELECT string_agg(sequence || ':' || encode(previous_digest, 'hex') || ':' || encode(digest, 'hex'), ',' ORDER BY sequence) FROM public.custody_event WHERE sequence <= {sequence}";

    private static Task<RestoreResult> LoadAsync(FidelityWorld destination, CopyFolder copy, RestoreTip tip) =>
        new RestoreLoader(destination.DataSource, new GuaranteeVerifier(destination.DataSource), TimeProvider.System)
            .LoadAsync(RestoreCopy.Open(copy.Path), tip, TestContext.Current.CancellationToken);

    private static JsonNode Spoiled(JsonNode each)
    {
        var digest = (string)each["digest"]!;
        each["digest"] = digest[..^1] + (digest[^1] == '0' ? '1' : '0');
        return each;
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
        var curator = CustodyAuthor.Person((long)(await world.InDomainAsync(1, "SELECT id FROM public.person WHERE name = 'zqx-active'"))!);
        var created = await world.Registry.CreateAsync(new NewPerson("zqx-flow", PersonFunctions.Curation), curator, token);
        var flow = created.PersonId!.Value;
        _ = await world.Credentials(workFactor: 1).SetPasswordAsync(flow, "senha-sintetica-zqx", curator, token);
        Assert.Equal(CustodyOutcome.Appended, (await new SignInAttempts(world.DataSource).AcceptedAsync(flow, IPAddress.Parse("203.0.113.9"), token)).Outcome);
        var first = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx", "texto sanitizado sintetico zqx"), token);
        _ = await world.Writer.AddReversalMapAsync(1, first, "mapa sintetico zqx", token);
        var second = await world.Writer.AddItemTextAsync(1, new NewItemText("segundo texto sintetico zqx"), token);
        _ = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, first), token);
        _ = await new ContentEmptier(world.DataSource).EmptyAsync(new ItemEmptying(CustodyAuthor.Person(flow), 1, second, EmptyingMotive.ContractRequirement, "PROT-2026/0042"), token);
        _ = await world.Registry.DeactivateAsync(flow, curator, token);
        _ = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(token);
        _ = await world.Verifier().VerifyAsync(token);
        return world;
    }
}
