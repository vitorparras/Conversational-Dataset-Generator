namespace Core.Platform.Tests.Tls;

/// <summary>A clock that stands at one moment, so that validity is judged against a time the test chose.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
