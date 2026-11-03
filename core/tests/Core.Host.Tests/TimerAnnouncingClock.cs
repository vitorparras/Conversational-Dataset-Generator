using Microsoft.Extensions.Time.Testing;

namespace Core.Host.Tests;

/// <summary>A clock the test moves that says when a timer has been created on it, so that time is not moved before whoever waits on it is waiting.</summary>
internal sealed class TimerAnnouncingClock : FakeTimeProvider
{
    private readonly TaskCompletionSource timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TimerAnnouncingClock(DateTimeOffset start)
        : base(start)
    {
    }

    public Task TimerCreated => timerCreated.Task;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        _ = timerCreated.TrySetResult();
        return timer;
    }
}
