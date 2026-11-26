using Microsoft.Extensions.Time.Testing;

namespace Core.Persistence.Tests.Reversal;

/// <summary>A clock that stands still at the moment it was made and tells when someone first waits on it; a wait ends only when the test moves it on.</summary>
internal sealed class PauseClock : TimeProvider
{
    private readonly FakeTimeProvider time = new(DateTimeOffset.UtcNow);
    private readonly TaskCompletionSource waited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the first wait starts.</summary>
    public Task Waited => waited.Task;

    /// <summary>How long the first wait was for; zero while no one waited.</summary>
    public TimeSpan Due { get; private set; }

    /// <summary>Moves the clock on, ending the waits that fall within.</summary>
    public void Advance(TimeSpan by) => time.Advance(by);

    public override DateTimeOffset GetUtcNow() => time.GetUtcNow();

    public override long GetTimestamp() => time.GetTimestamp();

    public override long TimestampFrequency => time.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => time.LocalTimeZone;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = time.CreateTimer(callback, state, dueTime, period);
        if (waited.TrySetResult())
        {
            Due = dueTime;
        }

        return timer;
    }
}
