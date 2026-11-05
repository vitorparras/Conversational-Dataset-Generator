namespace Core.Host.Tests;

/// <summary>
/// A clock that only moves when the test says so, and whose timers fire when it moves past them, so a delay is waited without sleeping.
/// It is safe between threads: one lock guards the time and the list of timers, and a callback never runs with the lock held.
/// </summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>How long until the next timer fires; null when no timer waits.</summary>
    public TimeSpan? NextDue
    {
        get
        {
            lock (gate)
            {
                return timers.Count == 0 ? null : timers.Min(timer => timer.Due) - now;
            }
        }
    }

    public int Waiting
    {
        get
        {
            lock (gate)
            {
                return timers.Count;
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate)
        {
            var timer = new ManualTimer(this, callback, state, now + dueTime);
            timers.Add(timer);
            return timer;
        }
    }

    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (gate)
        {
            now += by;
            var limit = now;
            due = timers.FindAll(timer => timer.Due <= limit);
            _ = timers.RemoveAll(timer => timer.Due <= limit);
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (gate)
        {
            _ = timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state, DateTimeOffset due) : ITimer
    {
        public DateTimeOffset Due { get; } = due;

        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose() => clock.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Fire() => callback(state);
    }
}
