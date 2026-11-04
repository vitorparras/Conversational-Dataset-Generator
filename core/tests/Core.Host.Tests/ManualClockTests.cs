namespace Core.Host.Tests;

/// <summary>The manual clock the delay and saturation tests share (#588): one lock for the timers, so threads that create, read, advance and dispose never corrupt the list.</summary>
public sealed class ManualClockTests
{
    private const int TestDeadlineMilliseconds = 60_000;
    private const int Rounds = 50;
    private const int Workers = 8;
    private const int TimersPerWorker = 40;
    private static readonly TimeSpan Soon = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan Far = TimeSpan.FromHours(1);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Fact(Timeout = TestDeadlineMilliseconds)]
    public async Task TimersMadeReadAdvancedAndDisposedOnDifferentThreadsLeaveTheRightCount()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var clock = new ManualClock();
            var fired = 0;
            var workers = Enumerable.Range(0, Workers)
                .Select(_ => Task.Run(() => Worked(clock, () => Interlocked.Increment(ref fired)), TestContext.Current.CancellationToken));

            var made = (await Task.WhenAll(workers)).SelectMany(timers => timers).ToList();
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(Workers * TimersPerWorker, fired);
            Assert.Equal(Workers * (TimersPerWorker / 2), clock.Waiting);
            Assert.NotNull(clock.NextDue);

            foreach (var timer in made)
            {
                timer.Dispose();
            }

            Assert.Equal(0, clock.Waiting);
            Assert.Null(clock.NextDue);
        }
    }

    [Fact(Timeout = TestDeadlineMilliseconds)]
    public void AdvanceFiresTheCallbackWithoutHoldingTheLock()
    {
        var clock = new ManualClock();
        int? seenByAnotherThread = null;
        using var timer = clock.CreateTimer(_ => seenByAnotherThread = WaitingSeenFromAnotherThread(clock), null, Soon, Timeout.InfiniteTimeSpan);

        clock.Advance(Soon);

        Assert.Equal(0, seenByAnotherThread);
    }

    [Fact(Timeout = TestDeadlineMilliseconds)]
    public void ADisposedTimerDoesNotFireAndAFiredOneLeavesTheList()
    {
        var clock = new ManualClock();
        var fired = 0;
        using var kept = clock.CreateTimer(_ => fired++, null, Soon, Timeout.InfiniteTimeSpan);
        using var disposed = clock.CreateTimer(_ => fired += 10, null, Soon, Timeout.InfiniteTimeSpan);
        disposed.Dispose();

        Assert.Equal(1, clock.Waiting);
        clock.Advance(Soon);

        Assert.Equal(1, fired);
        Assert.Equal(0, clock.Waiting);
    }

    private static List<ITimer> Worked(ManualClock clock, Action onFired)
    {
        var made = new List<ITimer>();
        for (var index = 0; index < TimersPerWorker; index++)
        {
            made.Add(clock.CreateTimer(_ => onFired(), null, Soon, Timeout.InfiniteTimeSpan));
            var distant = clock.CreateTimer(_ => onFired(), null, Far, Timeout.InfiniteTimeSpan);
            if (index % 2 == 0)
            {
                distant.Dispose();
            }

            made.Add(distant);
            _ = clock.Waiting;
            _ = clock.NextDue;
            _ = clock.GetUtcNow();
            clock.Advance(Soon);
        }

        return made;
    }

    private static int? WaitingSeenFromAnotherThread(ManualClock clock)
    {
        int? read = null;
        var reader = new Thread(() => read = clock.Waiting);
        reader.Start();
        return reader.Join(Patience) ? read : null;
    }
}
