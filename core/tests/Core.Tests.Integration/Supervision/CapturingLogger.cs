using Microsoft.Extensions.Logging;

namespace Core.Tests.Integration.Supervision;

/// <summary>Keeps every formatted message, so a test can look for what must never be in one.</summary>
internal sealed class CapturingLogger : ILogger
{
    private readonly List<string> messages = [];
    private readonly Lock gate = new();

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (gate)
            {
                return [.. messages];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        lock (gate)
        {
            messages.Add(formatter(state, exception) + (exception is null ? string.Empty : " | " + exception));
        }
    }
}
