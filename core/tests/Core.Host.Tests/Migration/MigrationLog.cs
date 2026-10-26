using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Migration;

/// <summary>Keeps every line the migration report writes: its event and the message a person reads.</summary>
internal sealed class MigrationLog : ILogger
{
    private readonly List<(int EventId, LogLevel Level, string Message)> lines = [];

    public IReadOnlyList<(int EventId, LogLevel Level, string Message)> Lines => lines;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        lines.Add((eventId.Id, logLevel, formatter(state, exception)));
}
