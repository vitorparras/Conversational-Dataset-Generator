namespace Core.Platform.Sessions;

/// <summary>The two limits of a session: how long it may go without activity, and how long it may last from the entry, whatever the activity.</summary>
public sealed class SessionLifetimes
{
    /// <summary>Both limits must be above zero.</summary>
    public SessionLifetimes(TimeSpan idle, TimeSpan absolute)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idle, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(absolute, TimeSpan.Zero);
        Idle = idle;
        Absolute = absolute;
    }

    /// <summary>The time without activity after which a session is refused; every use starts it again.</summary>
    public TimeSpan Idle { get; }

    /// <summary>The time after the entry at which a session is refused; nothing renews it.</summary>
    public TimeSpan Absolute { get; }
}
