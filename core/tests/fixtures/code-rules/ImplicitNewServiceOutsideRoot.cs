namespace Fixtures;

internal sealed class Clock;

internal sealed class Reporter
{
    private readonly Clock clock;

    public Reporter(Clock clock) => this.clock = clock;
}

internal sealed class Entry
{
    private readonly Clock clock = new();
    private readonly Reporter field = new(new Clock());

    public Reporter Property { get; } = new(new Clock());

    public static Reporter Build()
    {
        Reporter local = new(new Clock());
        return local;
    }
}
