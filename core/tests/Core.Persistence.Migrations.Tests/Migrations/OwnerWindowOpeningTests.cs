using System.Globalization;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Migrations;

/// <summary>
/// The opening of the window of the owner credential by the runner (#64): where the chain is verified (from the version that adds the column of its reference, 0016),
/// what a chain that does not hold stops,
/// when the event is written (before the first step, or right after the step that creates it in an older database) and when there is no window at all.
/// </summary>
[Trait("Category", "Database")]
public sealed class OwnerWindowOpeningTests
{
    private const string WindowStep = "0018-owner-window";
    private const string ChainBroken = "CE002";

    private const string Verified =
        "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'custody_chain', 0, 'verify_chain', 'verification_passed')";

    private const string VerifiedAsFailed =
        "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'custody_chain', 0, 'verify_chain', 'verification_failed')";

    private const string Break =
        "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; UPDATE public.custody_event SET item_id = item_id + 7 WHERE sequence = 1; "
        + "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";

    private const string Opened = "SELECT reference FROM public.custody_event WHERE step = 'owner_window_opened'";
    private const string Windows = "SELECT count(*)::text FROM public.custody_event WHERE step LIKE 'owner_window%'";
    private const string Events = "SELECT count(*)::text FROM public.custody_event";
    private const string HighestVersion = "SELECT coalesce(max(version), 0)::text FROM public.schema_version";

    private static int Creating => EmbeddedMigrations.Load().Single(step => step.Name == WindowStep).Version;
    private static int Last => EmbeddedMigrations.Load()[^1].Version;

    [DatabaseFact]
    public async Task DatabaseBelowSixteenKeepsTheTipWithoutVerifyingAndGetsTheWindowRightAfterTheStep()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(UpTo(15));
        await database.RunAsOwnerAsync(Verified);

        var result = await database.MigrateAsync(steps);

        Assert.Equal((MigrationOutcome.Applied, OwnerWindowOpening.OpenedAfterTheStep), (result.Outcome, result.OwnerWindow));
        Assert.Equal([$"from=15;to={steps[^1].Version};tip=1;digest={await DigestAsync(database, 1)};late=1"], await database.FirstColumnAsOwnerAsync(Opened));
        Assert.Equal(["0"], await database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.custody_event WHERE step = 'verify_chain' AND sequence > 1"));
    }

    [DatabaseFact]
    public async Task DatabaseOnSixteenVerifiesTheChainAndKeepsTheTipOfTheVerification()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(UpTo(16));
        await database.RunAsOwnerAsync(Verified);

        var result = await database.MigrateAsync(steps);

        Assert.Equal((MigrationOutcome.Applied, OwnerWindowOpening.OpenedAfterTheStep), (result.Outcome, result.OwnerWindow));
        Assert.Equal([$"from=16;to={steps[^1].Version};tip=2;digest={await DigestAsync(database, 2)};late=1"], await database.FirstColumnAsOwnerAsync(Opened));
    }

    [DatabaseFact]
    public async Task BrokenChainOnSixteenStopsBeforeTheFirstStepNamingTheChainAsTheCause()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(UpTo(16));
        await database.RunAsOwnerAsync(Verified);
        await database.ExecuteAsSuperuserAsync(Break);

        var result = await database.MigrateAsync(EmbeddedMigrations.Load());

        await AssertRefusedAsync(database, result, 16);
    }

    [DatabaseFact]
    public async Task BrokenChainJustBeforeTheStepStopsBeforeTheFirstStepNamingTheChainAsTheCause()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(UpTo(Creating - 1));
        await database.RunAsOwnerAsync(Verified);
        await database.ExecuteAsSuperuserAsync(Break);

        var result = await database.MigrateAsync(EmbeddedMigrations.Load());

        await AssertRefusedAsync(database, result, Creating - 1);
    }

    [DatabaseFact]
    public async Task BrokenChainOnTheStepOrLaterStopsBeforeTheFirstStepAndAppliesNothing()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync(Verified);
        await database.ExecuteAsSuperuserAsync(Break);

        var result = await database.MigrateAsync(WithSynthetic());

        await AssertRefusedAsync(database, result, Last);
        Assert.False(await database.TableExistsAsync("zqx_after"));
    }

    [DatabaseFact]
    public async Task DatabaseOnTheStepOrLaterGetsTheWindowBeforeTheFirstStepWithTheTipOfTheVerification()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync(Verified);

        var result = await database.MigrateAsync(WithSynthetic());

        Assert.Equal((MigrationOutcome.Applied, OwnerWindowOpening.Opened), (result.Outcome, result.OwnerWindow));
        Assert.Equal([$"from={Last};to={Last + 1};tip=2;digest={await DigestAsync(database, 2)}"], await database.FirstColumnAsOwnerAsync(Opened));
        Assert.True(await database.TableExistsAsync("zqx_after"));
    }

    [DatabaseFact]
    public async Task WindowThatTheChainRefusesRightAfterTheStepEndsTheRunNamingTheChainAndTheStepsApplied()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var steps = EmbeddedMigrations.Load();
        _ = await database.MigrateAsync(UpTo(15));
        await database.RunAsOwnerAsync(VerifiedAsFailed);

        var result = await database.MigrateAsync(steps);

        Assert.Equal((MigrationOutcome.StepFailed, WindowStep, ChainBroken, OwnerWindowOpening.None), (result.Outcome, result.Step, result.SqlState, result.OwnerWindow));
        Assert.Equal(steps.Skip(15).Take(Creating - 15).Select(step => step.Name), result.Applied);
        Assert.Equal((Creating, steps[^1].Version), (result.Found, result.Expected));
        Assert.Equal(["0"], await database.FirstColumnAsOwnerAsync(Windows));
    }

    [DatabaseFact]
    public async Task BuildWithoutTheStepOpensNoWindow()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var without = EmbeddedMigrations.Load().Where(step => step.Name != WindowStep).ToList();
        _ = await database.MigrateAsync(UpTo(15));

        var result = await database.MigrateAsync(without);

        Assert.Equal((MigrationOutcome.Applied, OwnerWindowOpening.None), (result.Outcome, result.OwnerWindow));
        Assert.Equal(["0"], await database.FirstColumnAsOwnerAsync(Windows));
    }

    [DatabaseFact]
    public async Task EmptyDatabaseHasNoChainAndGetsNoWindowNotEvenAfterTheStep()
    {
        await using var database = await ScratchDatabase.CreateAsync();

        var result = await database.MigrateAsync(EmbeddedMigrations.Load());

        Assert.Equal((MigrationOutcome.Applied, OwnerWindowOpening.None), (result.Outcome, result.OwnerWindow));
        Assert.Equal(["0"], await database.FirstColumnAsOwnerAsync(Events));
    }

    [DatabaseFact]
    public async Task RunWithNothingPendingOpensNoWindow()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());

        var result = await database.MigrateAsync(EmbeddedMigrations.Load());

        Assert.Equal((MigrationOutcome.NothingToDo, OwnerWindowOpening.None), (result.Outcome, result.OwnerWindow));
        Assert.Equal(["0"], await database.FirstColumnAsOwnerAsync(Events));
    }

    private static async Task AssertRefusedAsync(ScratchDatabase database, MigrationResult result, int found)
    {
        Assert.Equal((MigrationOutcome.StepFailed, WindowStep, ChainBroken, OwnerWindowOpening.None), (result.Outcome, result.Step, result.SqlState, result.OwnerWindow));
        Assert.Empty(result.Applied);
        Assert.Equal(found, result.Found);
        Assert.Equal([found.ToString(CultureInfo.InvariantCulture)], await database.FirstColumnAsOwnerAsync(HighestVersion));
        Assert.Equal(["0"], await database.FirstColumnAsOwnerAsync(Windows));
    }

    private static async Task<string> DigestAsync(ScratchDatabase database, int sequence) =>
        Assert.Single(await database.FirstColumnAsOwnerAsync($"SELECT encode(digest, 'hex') FROM public.custody_event WHERE sequence = {sequence}"));

    private static List<MigrationStep> UpTo(int version) => EmbeddedMigrations.Load().Where(step => step.Version <= version).ToList();

    private static IReadOnlyList<MigrationStep> WithSynthetic() => TestSteps.OnTopOfTheEmbedded(("zqx-after", "CREATE TABLE public.zqx_after (a integer)"));
}
