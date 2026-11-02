using System.Collections.Concurrent;
using Core.Persistence.ServerGuarantees;

namespace Core.Host.Tests;

/// <summary>A periodic comparison running over a plan whose answers the test gives, on a clock the test moves, with the intervals it was asked about kept.</summary>
internal sealed class AuditComparisonRun : IAsyncDisposable
{
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private static readonly TimeSpan WaitFor = TimeSpan.FromSeconds(20);

    private readonly AuditComparisonService service;
    private readonly ConcurrentQueue<(DateTimeOffset From, DateTimeOffset To)> calls;

    private AuditComparisonRun(
        AuditComparisonService service,
        TimerAnnouncingClock clock,
        AuditComparisonState state,
        AuditComparisonLog log,
        ConcurrentQueue<(DateTimeOffset From, DateTimeOffset To)> calls)
    {
        this.service = service;
        Clock = clock;
        State = state;
        Log = log;
        this.calls = calls;
    }

    public TimerAnnouncingClock Clock { get; }

    public AuditComparisonState State { get; }

    public AuditComparisonLog Log { get; }

    public IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> Calls => [.. calls];

    public static async Task<AuditComparisonRun> StartAsync(Func<int, IReadOnlyList<AuditDifference>> answer)
    {
        var calls = new ConcurrentQueue<(DateTimeOffset From, DateTimeOffset To)>();
        var clock = new TimerAnnouncingClock(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        var state = new AuditComparisonState();
        var log = new AuditComparisonLog();
        var plan = new AuditComparisonPlan(
            Interval,
            (from, to, _) =>
            {
                var index = calls.Count;
                calls.Enqueue((from, to));
                return Task.FromResult(answer(index));
            },
            log);
        var service = new AuditComparisonService(plan, state, clock);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await clock.TimerCreated.WaitAsync(WaitFor, TestContext.Current.CancellationToken);
        return new AuditComparisonRun(service, clock, state, log, calls);
    }

    /// <summary>Lets one interval pass and waits for the comparison it starts to be recorded, whatever it found.</summary>
    public async Task<AuditComparisonStatus> NextRoundAsync()
    {
        var before = State.Current;
        Clock.Advance(Interval);
        var deadline = DateTimeOffset.UtcNow + WaitFor;
        while (ReferenceEquals(State.Current, before))
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The comparison did not end in the time given.");
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }

        return State.Current;
    }

    public async ValueTask DisposeAsync()
    {
        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }
}
