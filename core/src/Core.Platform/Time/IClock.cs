namespace Core.Platform.Time;

/// <summary>The time the process reads. A port, so that tests and the custody chain can fix it.</summary>
public interface IClock
{
    /// <summary>The current instant, in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>The machine clock.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();
}
