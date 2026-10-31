namespace Core.Platform.Tests.Sessions;

internal sealed class AdjustableClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan time) => now += time;

    public override DateTimeOffset GetUtcNow() => now;
}
