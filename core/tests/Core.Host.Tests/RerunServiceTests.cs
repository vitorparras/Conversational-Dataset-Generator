using Core.Boundary.Text;
using Core.Host.Tests.Submissions;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Domains;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The worker that empties the queue of the rerun (issue 97) in the background: it treats the items of requests to the version of its steps and leaves the others; an item that fails
/// at every try is refused and the rest goes on; on start it takes up what a stopped process left; a database that drops keeps what was treated; and its log names the domain, the item
/// and the outcome, never a word of the material. Built by the host, it gives the item back when the key is not read; with no database it does nothing.
/// </summary>
public sealed class RerunServiceTests
{
    private const string Second = "ZQXSEGUNDO";

    [DatabaseFact]
    public async Task WorkerTreatsEveryQueuedItemOfItsVersion()
    {
        await using var world = await RerunWorld.CreateAsync();
        var rerun = await world.RequestAsync(CompositionRoot.StepsOf());
        await world.StartAsync(InForce());

        await world.IdleAsync();

        var progress = await world.ProgressAsync(rerun);
        Assert.Equal((0L, 0L, 2L, 0L), (progress.Queued, progress.Held, progress.Identical + progress.Replaced, progress.Refused));
        Assert.Equal(CompositionRoot.StepsOf().Version, await world.VersionAsync());
    }

    [DatabaseFact]
    public async Task WorkerRerunsTheRawTextAndNotTheSanitizedOne()
    {
        // The first letter of each text is a token, which both lists remove: from the raw text the steps in force make again the text held.
        await using var world = await RerunWorld.CreateAsync(Analysis.AnsweringRecognizer.Finding("TOKEN"));
        var rerun = await world.RequestAsync(CompositionRoot.StepsOf());
        await world.StartAsync(InForce());

        await world.IdleAsync();

        var progress = await world.ProgressAsync(rerun);
        Assert.Equal((2L, 0L, 0L), (progress.Identical, progress.Replaced, progress.Refused));
    }

    [DatabaseFact]
    public async Task WorkerLeavesTheItemsOfAnotherTargetUntaken()
    {
        await using var world = await RerunWorld.CreateAsync();
        var rerun = await world.RequestAsync(new SanitizationSteps([new IdentifierStep(new TextBoundaries()), new DestinationsStep()]));
        await world.StartAsync(InForce());

        await world.IdleAsync();

        var progress = await world.ProgressAsync(rerun);
        Assert.Equal((2L, 0L, 0L), (progress.Queued, progress.Held, progress.Identical + progress.Replaced + progress.Refused));
        Assert.Equal(RerunWorld.Before.Version, await world.VersionAsync());
    }

    [DatabaseFact]
    public async Task WorkerRefusesAnItemThatKeepsFailingAndGoesOn()
    {
        await using var world = await RerunWorld.CreateAsync();
        var failing = SanitizingStep.FailingOn(Second);
        var rerun = await world.RequestAsync(new SanitizationSteps([failing]));
        await world.StartAsync(new SanitizationRerun(failing.Alone()));

        await world.IdleAsync();

        var progress = await world.ProgressAsync(rerun);
        Assert.Equal((0L, 1L, 1L), (progress.Queued + progress.Held, progress.Identical + progress.Replaced, progress.Refused));
        Assert.Equal(RerunRefusal.Failed, Assert.Single(progress.Refusals).Refusal);
        Assert.Equal(SanitizationReruns.Tries, world.Log.Lines.Count(line => line.Contains(nameof(SanitizationStepFailedException), StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task WorkerPicksUpTheQueueLeftByAStoppedProcess()
    {
        await using var world = await RerunWorld.CreateAsync();
        var rerun = await world.RequestAsync(CompositionRoot.StepsOf());
        _ = Assert.NotNull(await world.Queue.TakeAsync(SubmissionWorld.Domain, CompositionRoot.StepsOf().Version, TestContext.Current.CancellationToken));
        await world.StartAsync(InForce());
        await world.IdleAsync();
        var waiting = await world.ProgressAsync(rerun);

        await world.PastTheHoldAsync();

        var progress = await world.ProgressAsync(rerun);
        Assert.Equal((0L, 1L, 1L), (waiting.Queued, waiting.Held, waiting.Identical + waiting.Replaced));
        Assert.Equal((0L, 0L, 2L), (progress.Queued, progress.Held, progress.Identical + progress.Replaced));
    }

    [DatabaseFact]
    public async Task WorkerKeepsTheTreatedItemsWhenTheDatabaseDrops()
    {
        await using var world = await RerunWorld.CreateAsync();
        await using var gone = NpgsqlDataSource.Create(UnreachableDatabase.ConnectionString);
        var rerun = await world.RequestAsync(CompositionRoot.StepsOf());
        var opened = 0;
        await world.StartAsync(InForce(), async token =>
        {
            if (Interlocked.Increment(ref opened) == 2)
            {
                _ = await RegisteredLabels.ReadAsync(gone, token);
            }

            return await world.OpenAsync(token);
        });

        await world.IdleAsync();

        var progress = await world.ProgressAsync(rerun);
        Assert.Equal((0L, 1L, 1L), (progress.Queued, progress.Held, progress.Identical + progress.Replaced));
        Assert.Contains(world.Log.Lines, line => line.StartsWith("Error:", StringComparison.Ordinal) && line.Contains(nameof(NpgsqlException), StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task WorkerLogsTheDomainTheItemAndTheOutcomeButNoWordOfTheMaterial()
    {
        await using var world = await RerunWorld.CreateAsync();
        _ = await world.RequestAsync(CompositionRoot.StepsOf());
        await world.StartAsync(InForce());

        await world.IdleAsync();

        var lines = world.Log.Lines;
        Assert.Equal(2, lines.Count(line => line.Contains($"do domínio {SubmissionWorld.Domain}", StringComparison.Ordinal)));
        Assert.All(lines, line => Assert.DoesNotContain("ZQX", line, StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task WorkerOfTheHostGivesTheItemBackWhenTheKeyIsNotRead()
    {
        await using var world = await RerunWorld.CreateAsync();
        var rerun = await world.RequestAsync(CompositionRoot.StepsOf());
        await world.StartInHostAsync(new FailingKeySource(MasterKeyFailureKind.Absent), InForce());

        await world.IdleAsync();

        var progress = await world.ProgressAsync(rerun);
        Assert.Equal((2L, 0L, 0L), (progress.Queued, progress.Held, progress.Identical + progress.Replaced + progress.Refused));
        Assert.Contains(world.Log.Lines, line => line.StartsWith("Error:", StringComparison.Ordinal) && line.Contains("chave mestra não foi lida", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithNoDatabaseTheWorkerDoesNothingAndEnds()
    {
        using var loggers = new LoggerFactory();
        using var worker = new RerunService(null, new ManualClock(), loggers);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
    }

    /// <summary>The rerun by the steps the host declares, as the composition root builds it.</summary>
    private static SanitizationRerun InForce() => new(new Sanitizer(CompositionRoot.StepsOf(), new TextBoundaries()));
}
