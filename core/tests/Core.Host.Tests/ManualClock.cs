namespace Core.Host.Tests;

/// <summary>A clock that only moves when the test says so, and whose timers fire when it moves past them, so a delay is waited without sleeping.</summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>How long until the next timer fires; null when no timer waits.</summary>
    public TimeSpan? NextDue => timers.Count == 0 ? null : timers.Min(timer => timer.Due) - now;

    public int Waiting => timers.Count;

    public override DateTimeOffset GetUtcNow() => now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state, now + dueTime);
        timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        now += by;
        foreach (var due in timers.Where(timer => timer.Due <= now).ToList())
        {
            _ = timers.Remove(due);
            due.Fire();
        }
    }

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state, DateTimeOffset due) : ITimer
    {
        public DateTimeOffset Due { get; } = due;

        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose() => _ = clock.timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Fire() => callback(state);
    }
}
