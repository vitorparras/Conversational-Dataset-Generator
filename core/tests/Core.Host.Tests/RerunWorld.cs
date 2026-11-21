using System.Diagnostics.CodeAnalysis;
using Core.Boundary.Text;
using Core.Host.Tests.Submissions;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Platform.Keys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>
/// The worker of the rerun (issue 97) over a database of its own: one accepted batch of two items, sanitized by a list of steps older than the one in force, requests made by the
/// queue of production, and the worker run over a clock the test moves, with the content opened as the test says and its log kept.
/// </summary>
internal sealed class RerunWorld : IAsyncDisposable
{
    /// <summary>The list the items were sanitized with: the destinations alone.</summary>
    internal static readonly SanitizationSteps Before = new([new DestinationsStep()]);

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private readonly LoggerFactory loggers;
    private RerunService? worker;
    private ServiceProvider? host;

    private RerunWorld(SubmissionWorld submissions, long batch)
    {
        Submissions = submissions;
        Batch = batch;
        Clock.Advance(DateTimeOffset.UtcNow - Clock.GetUtcNow());
        Queue = new SanitizationReruns(submissions.DataSource, Clock);
        loggers = new LoggerFactory([Log]);
    }

    internal SubmissionWorld Submissions { get; }

    internal long Batch { get; }

    internal ManualClock Clock { get; } = new();

    internal RecordedLog Log { get; } = new();

    internal SanitizationReruns Queue { get; }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [SuppressMessage("Reliability", "CA2000", Justification = "The world takes the submissions and lets go of them with itself.")]
    internal static async Task<RerunWorld> CreateAsync()
    {
        var submissions = await SubmissionWorld.CreateAsync(sanitizer: new Sanitizer(Before, new TextBoundaries()));
        var outcome = await submissions.SubmitAsync(SubmissionWorld.Request());
        await submissions.AcceptReviewedAsync(outcome.Batch!.Value);
        return new RerunWorld(submissions, outcome.Batch.Value);
    }

    /// <summary>The steps one name@version per line, as the request carries them.</summary>
    internal static string Listed(SanitizationSteps steps) => string.Join("\n", steps.Order.Select(step => $"{step.Name}@{step.Version}"));

    /// <summary>The request of the person of the world to rerun the items of the domain to the steps given, which must queue them.</summary>
    internal async Task<long> RequestAsync(SanitizationSteps steps)
    {
        var asked = await Queue.RequestAsync(new RerunRequest(SubmissionWorld.Domain, SubmissionWorld.Person, steps.Version, Listed(steps)), Token);
        Assert.Equal(RerunOutcome.Queued, asked.Outcome);
        return asked.RerunId!.Value;
    }

    /// <summary>Where the request stands, read by the queue of production.</summary>
    internal async Task<RerunProgress> ProgressAsync(long rerun) =>
        await Queue.ProgressAsync(SubmissionWorld.Domain, rerun, Token) ?? throw new InvalidOperationException("The request was not found.");

    /// <summary>The version the detail of the batch names, read by the counts of production.</summary>
    internal async Task<string?> VersionAsync() => (await new BatchCounts(Submissions.DataSource).ReadAsync(SubmissionWorld.Domain, Batch, Token)).SanitizationVersion;

    /// <summary>The content of the world opened for the worker, as the host opens it with the key.</summary>
    internal Task<RerunService.Access> OpenAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new RerunService.Access(new ItemResanitizer(Submissions.DataSource, Submissions.Content, Clock), null));

    /// <summary>Starts the worker of the rerun given, with the content opened by the function given or, by default, as the host opens it.</summary>
    internal Task StartAsync(SanitizationRerun rerun, Func<CancellationToken, Task<RerunService.Access>>? open = null)
    {
        var parts = new RerunService.Parts(new DomainListing(Submissions.DataSource), new SanitizationReruns(Submissions.DataSource, Clock), rerun, open ?? OpenAsync);
        worker = new RerunService(parts, Clock, loggers.CreateLogger<RerunService>());
        return worker.StartAsync(Token);
    }

    /// <summary>Starts the worker as the host builds it, over the database of the world and the key the source given reads.</summary>
    internal Task StartInHostAsync(IMasterKeySource key, SanitizationRerun rerun)
    {
        host = new ServiceCollection()
            .AddSingleton(new ServiceCollection().AddSingleton(Submissions.DataSource).AddSingleton(key).BuildServiceProvider())
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton(loggers.CreateLogger<RerunService>())
            .BuildServiceProvider();
        worker = new RerunService(host, rerun);
        return worker.StartAsync(Token);
    }

    /// <summary>Waits until the worker waits, which is when no domain had an item for it.</summary>
    internal async Task IdleAsync()
    {
        var until = DateTimeOffset.UtcNow + Patience;
        while (Clock.Waiting == 0 && DateTimeOffset.UtcNow < until)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), Token);
        }

        Assert.Equal((1, (TimeSpan?)TimeSpan.FromSeconds(5)), (Clock.Waiting, Clock.NextDue));
    }

    /// <summary>Moves the clock past the hold of an item, which also ends the pause of the worker, and waits until it waits again.</summary>
    internal async Task PastTheHoldAsync()
    {
        Clock.Advance(SanitizationReruns.Hold + TimeSpan.FromSeconds(1));
        await IdleAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (worker is not null)
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        if (host is not null)
        {
            await host.GetRequiredService<ServiceProvider>().DisposeAsync();
            await host.DisposeAsync();
        }

        loggers.Dispose();
        await Submissions.DisposeAsync();
    }
}
