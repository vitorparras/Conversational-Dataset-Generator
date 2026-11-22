using System.Globalization;
using Core.Host.Submissions;
using Core.Host.Tests.Submissions;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// The host built as it is installed takes up the queue of the rerun (issue 97) when it starts: an item held by a process that stopped is taken again once its hold expired, and an item already
/// concluded is not treated again. It is the worker the root registers that does it, over the content it opens with the key of the installation.
/// </summary>
[Trait("Category", "Database")]
public sealed class RerunRestartTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);

    [DatabaseFact]
    public async Task RerunResumesAfterTheProcessRestarts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await RerunWorld.CreateAsync();
        var (rerun, concluded, left) = await StoppedHalfwayAsync(world, cancellationToken);
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with { Environment = MigratedDatabase.VariablesOf(world.Submissions.Database) };

        using (await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken))
        {
            await UntilNothingWaitsAsync(world, rerun);
        }

        var progress = await world.ProgressAsync(rerun);
        Assert.Equal((1L, 1L, 0L, 0L), (progress.Identical + progress.Replaced, progress.Refused, progress.Queued, progress.Held));
        Assert.Equal([new RerunItemRefusal(left, world.Batch, RerunRefusal.Emptied)], progress.Refusals);
        Assert.Equal(["1"], await world.Submissions.Database.FirstColumnAsOwnerAsync(
            $"SELECT count(*)::text FROM public.custody_event WHERE step = 'item_resanitized' AND item_kind = 'item_text' AND item_id = {concluded}"));
    }

    /// <summary>
    /// What a process that stopped ten minutes ago left: the request, one item it concluded and one it held, whose hold expired since. The held one is emptied, so that the host refuses it without
    /// opening its text, which the key of the installation does not open.
    /// </summary>
    private static async Task<(long Rerun, long Concluded, long Left)> StoppedHalfwayAsync(RerunWorld world, CancellationToken cancellationToken)
    {
        var past = new ManualClock();
        past.Advance(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10) - past.GetUtcNow());
        var source = world.Submissions.DataSource;
        var queue = new SanitizationReruns(source, past);
        var steps = CompositionRoot.StepsOf();
        var asked = await queue.RequestAsync(new RerunRequest(SubmissionWorld.Domain, SubmissionWorld.Person, steps.Version, RerunWorld.Listed(steps)), cancellationToken);
        var first = await queue.TakeAsync(SubmissionWorld.Domain, steps.Version, cancellationToken) ?? throw new InvalidOperationException("No item was queued.");
        _ = await new ItemResanitizer(source, world.Submissions.Content, past).ResanitizeAsync(first, held => DecidedItemTexts.Rerun(RerunScreensWorld.InForce, held), cancellationToken);
        var second = await queue.TakeAsync(SubmissionWorld.Domain, steps.Version, cancellationToken) ?? throw new InvalidOperationException("No second item was queued.");
        var curator = await CuratorAsync(world);
        var emptied = await new ContentEmptier(source).EmptyAsync(
            new ItemEmptying(CustodyAuthor.Person(curator), SubmissionWorld.Domain, second.ItemId, EmptyingMotive.RemovalRequestedBySubject, "PROT-2026/0097"), cancellationToken);
        Assert.Equal(EmptyingOutcome.Emptied, emptied.Outcome);
        return (asked.RerunId!.Value, first.ItemId, second.ItemId);
    }

    /// <summary>A person of the curation, who empties the item.</summary>
    private static async Task<long> CuratorAsync(RerunWorld world)
    {
        await world.Submissions.Database.RunAsOwnerAsync("INSERT INTO public.person (name, function_name) VALUES ('zqx-curadora', 'Curadoria do acervo')");
        var id = await world.Submissions.Database.FirstColumnAsOwnerAsync("SELECT id::text FROM public.person WHERE name = 'zqx-curadora'");
        return long.Parse(id[0], CultureInfo.InvariantCulture);
    }

    private static async Task UntilNothingWaitsAsync(RerunWorld world, long rerun)
    {
        var until = DateTimeOffset.UtcNow + Patience;
        while ((await world.ProgressAsync(rerun)) is { } progress && progress.Queued + progress.Held > 0)
        {
            Assert.True(DateTimeOffset.UtcNow < until, "The host did not take up the queue.");
            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }
    }
}
