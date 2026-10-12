namespace Fixtures;

internal sealed class Clock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}

internal sealed class Reporter
{
    private readonly Clock clock;

    public Reporter(Clock clock) => this.clock = clock;

    public string Report() => clock.Now.ToString("O", CultureInfo.InvariantCulture);
}

internal static class Entry
{
    public static string Run() => new Reporter(new Clock()).Report();
}
