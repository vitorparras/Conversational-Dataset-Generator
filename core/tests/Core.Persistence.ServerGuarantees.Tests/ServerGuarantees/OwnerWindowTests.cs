using System.Text;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The window of the owner credential (#64, CA-1 to CA-4): a migration with a step pending opens it in the chain before the first step, with what it is for
/// and the tip it found; the next start closes it with the verdict of the chain and of the guarantees; a guarantee that a step took away makes the closing
/// fail and the start be refused, naming it; and only the role of the application starts.
/// </summary>
[Trait("Category", "Database")]
public sealed class OwnerWindowTests
{
    private const string OwnerWindowStep = "0018-owner-window";

    private const string ClosedPointsAtTheEventsBefore =
        "SELECT (c.reference = format('opened=%s;applied=%s;chain=%s;conference=%s', o.sequence, (SELECT max(version) FROM public.schema_version), "
        + "(SELECT max(sequence) FROM public.custody_event WHERE step = 'verify_chain'), "
        + "(SELECT max(sequence) FROM public.custody_event WHERE step = 'verify_guarantees' AND sequence < c.sequence)))::text "
        + "FROM public.custody_event c, public.custody_event o WHERE c.step = 'owner_window_closed' AND o.step = 'owner_window_opened'";

    private const string Windows =
        "SELECT step || '|' || reason || '|' || item_id || '|' || recorded_by || '|' || actor_system FROM public.custody_event WHERE step LIKE 'owner_window%' ORDER BY sequence";

    private const string Registry = "SELECT string_agg(version || ':' || sha256, ',' ORDER BY version) FROM public.schema_version";
    private const string Opened = "SELECT sequence || '|' || reference FROM public.custody_event WHERE step = 'owner_window_opened'";
    private const string LastChainVerification = "SELECT sequence || '|' || encode(digest, 'hex') FROM public.custody_event WHERE step = 'verify_chain' ORDER BY sequence DESC LIMIT 1";

    [DatabaseFact]
    public async Task MigrationWithAStepPendingOpensTheWindowBeforeTheFirstStepWithWhatForAndTheTipItFound()
    {
        await using var world = await FidelityWorld.CreateAsync();
        var left = EmbeddedMigrations.Load()[^1].Version;

        var result = await world.MigrateAsync(WithSynthetic("CREATE TABLE public.zqx_after (a integer)"));

        var tip = Assert.Single(await world.OwnerColumnAsync(LastChainVerification)).Split('|');
        var opened = Assert.Single(await world.OwnerColumnAsync(Opened)).Split('|');
        Assert.Equal((MigrationOutcome.Applied, OwnerWindowOpening.Opened), (result.Outcome, result.OwnerWindow));
        Assert.Equal(long.Parse(tip[0], System.Globalization.CultureInfo.InvariantCulture) + 1, long.Parse(opened[0], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal($"from={left};to={left + 1};tip={tip[0]};digest={tip[1]}", opened[1]);
        Assert.Equal([$"owner_window_opened|administrative_act|{left + 1}|owner|true"], await world.OwnerColumnAsync(Windows));
        Assert.Equal(
            ["true"],
            await world.OwnerColumnAsync(
                $"SELECT ((SELECT occurred_at FROM public.custody_event WHERE step = 'owner_window_opened') < (SELECT applied_at FROM public.schema_version WHERE version = {left + 1}))::text"));
    }

    [DatabaseFact]
    public async Task RunWithNothingToApplyOpensNoWindow()
    {
        await using var world = await FidelityWorld.CreateAsync();

        var result = await world.MigrateAsync(EmbeddedMigrations.Load());

        Assert.Equal((MigrationOutcome.NothingToDo, OwnerWindowOpening.None), (result.Outcome, result.OwnerWindow));
        Assert.Empty(await world.OwnerColumnAsync(Windows));
        Assert.Empty(await world.OwnerColumnAsync("SELECT sequence::text FROM public.custody_event"));
    }

    [DatabaseFact]
    public async Task EmptyDatabaseHasNoChainToGuardAndOpensNoWindow()
    {
        await using var database = await ScratchDatabase.CreateAsync();

        var result = await database.MigrateAsync(EmbeddedMigrations.Load());

        Assert.Equal((MigrationOutcome.Applied, OwnerWindowOpening.None), (result.Outcome, result.OwnerWindow));
        Assert.Empty(await database.FirstColumnAsOwnerAsync("SELECT sequence::text FROM public.custody_event"));
    }

    [DatabaseFact]
    public async Task DatabaseOlderThanTheStepGetsTheWindowRightAfterItWithTheTipReadBefore()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();
        var older = steps.Where(step => step.Name != OwnerWindowStep).ToList();
        _ = await database.MigrateAsync(older);
        await database.RunAsOwnerAsync(
            "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'custody_chain', 0, 'verify_chain', 'verification_passed')");

        var result = await database.MigrateAsync(steps);

        var opened = Assert.Single(await database.FirstColumnAsOwnerAsync(Opened)).Split('|');
        var tipBefore = Assert.Single(await database.FirstColumnAsOwnerAsync(LastChainVerification)).Split('|');
        Assert.Equal("2", tipBefore[0]);
        Assert.Equal((MigrationOutcome.Applied, OwnerWindowOpening.OpenedAfterTheStep), (result.Outcome, result.OwnerWindow));
        Assert.Equal($"from={older[^1].Version};to={steps[^1].Version};tip={tipBefore[0]};digest={tipBefore[1]};late=1", opened[1]);
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var chain = await new CustodyVerifier(source, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.True(chain.Passed);
    }

    [DatabaseFact]
    public async Task NextStartClosesTheWindowAfterVerifyingTheChainAndConferringTheGuarantees()
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        var left = EmbeddedMigrations.Load()[^1].Version;
        _ = await world.MigrateAsync(WithSynthetic("CREATE TABLE public.zqx_after (a integer)"));
        var reader = new RecordedVerificationReader(world.DataSource);
        var open = await reader.OwnerWindowAsync(token);

        var result = await new OwnerWindowCheck(world.DataSource, world.Verifier(), TimeProvider.System).RunAsync(token);
        var again = await new OwnerWindowCheck(world.DataSource, world.Verifier(), TimeProvider.System).RunAsync(token);

        Assert.NotNull(open);
        Assert.Equal((left, left + 1, false, false), (open.From, open.To, open.AfterTheStep, open.ClosingFailed));
        Assert.Equal((CheckStatus.Passed, CheckStatus.Passed), (result.Status, again.Status));
        Assert.Equal(
            [$"owner_window_opened|administrative_act|{left + 1}|owner|true", "owner_window_closed|verification_passed|0|app|true"],
            await world.OwnerColumnAsync(Windows));
        Assert.Equal(["true"], await world.OwnerColumnAsync(ClosedPointsAtTheEventsBefore));
        Assert.Null(await reader.OwnerWindowAsync(token));
    }

    [DatabaseTheory]
    [InlineData("ALTER TABLE content.item_text NO FORCE ROW LEVEL SECURITY", GuaranteeKinds.RowPolicy)]
    [InlineData("ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed", GuaranteeKinds.Immutability)]
    public async Task StepThatTookAGuaranteeAwayMakesTheClosingFailAndTheStartRefusedNamingIt(string removal, GuaranteeKinds missing)
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        var left = EmbeddedMigrations.Load()[^1].Version;
        _ = await world.MigrateAsync(WithSynthetic(removal));
        var check = new OwnerWindowCheck(world.DataSource, world.Verifier(), TimeProvider.System);

        var result = await check.RunAsync(token);
        var open = await new RecordedVerificationReader(world.DataSource).OwnerWindowAsync(token);
        var retried = await check.RunAsync(token);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains(missing.ToString(), result.Message, StringComparison.Ordinal);
        Assert.Equal(CheckStatus.Failed, retried.Status);
        Assert.NotNull(open);
        Assert.True(open.ClosingFailed);
        var failed = $"owner_window_closed|verification_failed|{(long)missing}|app|true";
        Assert.Equal([$"owner_window_opened|administrative_act|{left + 1}|owner|true", failed, failed], await world.OwnerColumnAsync(Windows));
    }

    [DatabaseFact]
    public async Task StartWithNoWindowOpenWritesNothingAndPasses()
    {
        await using var world = await FidelityWorld.CreateAsync();

        var result = await new OwnerWindowCheck(world.DataSource, world.Verifier(), TimeProvider.System).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Empty(await world.OwnerColumnAsync("SELECT sequence::text FROM public.custody_event"));
    }

    [DatabaseFact]
    public async Task MigrationOverABrokenChainStopsBeforeTheFirstStepAndRebuildsNothing()
    {
        await using var world = await FidelityWorld.CreateAsync();
        await world.RunAsOwnerAsync("INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'custody_chain', 0, 'verify_chain', 'verification_passed')");
        await BreakTheChainAsync(world);
        var before = await world.OwnerColumnAsync(Registry);

        var result = await world.MigrateAsync(WithSynthetic("CREATE TABLE public.zqx_after (a integer)"));

        Assert.Equal((MigrationOutcome.StepFailed, OwnerWindowStep, OwnerWindowOpening.None), (result.Outcome, result.Step, result.OwnerWindow));
        Assert.Empty(result.Applied);
        Assert.Equal(["0"], await world.OwnerColumnAsync("SELECT count(*)::text FROM pg_tables WHERE tablename = 'zqx_after'"));
        Assert.Equal(before, await world.OwnerColumnAsync(Registry));
        Assert.Empty(await world.OwnerColumnAsync(Windows));
    }

    [DatabaseFact]
    public async Task BrokenChainAfterTheWindowOpenedRefusesTheClosingAndTheStartAndLeavesTheWindowOpen()
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        _ = await world.MigrateAsync(WithSynthetic("CREATE TABLE public.zqx_after (a integer)"));
        await BreakTheChainAsync(world);

        var result = await new OwnerWindowCheck(world.DataSource, world.Verifier(), TimeProvider.System).RunAsync(token);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.NotNull(await new RecordedVerificationReader(world.DataSource).OwnerWindowAsync(token));
        Assert.DoesNotContain(await world.OwnerColumnAsync(Windows), line => line.StartsWith("owner_window_closed", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task StartWithTheOwnerRoleIsRefusedByThePrivilegeCheck()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var asOwner = NpgsqlDataSource.Create(database.OwnerConnectionString);

        var result = await new PrivilegeCheck(asOwner).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }


    private static Task BreakTheChainAsync(FidelityWorld world) =>
        world.RunAsSuperuserAsync(
            "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; UPDATE public.custody_event SET item_id = item_id + 7 WHERE sequence = 1; "
            + "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed");

    private static List<MigrationStep> WithSynthetic(string sql)
    {
        var steps = EmbeddedMigrations.Load().ToList();
        var version = steps[^1].Version + 1;
        steps.Add(MigrationStep.Of(version, $"{version:0000}-zqx-synthetic", Encoding.UTF8.GetBytes(sql)));
        return steps;
    }
}
