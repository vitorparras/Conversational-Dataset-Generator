using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>Keeps every line the periodic comparison writes, with its level, formatted as the console would show it, from whichever thread writes it.</summary>
internal sealed class AuditComparisonLog : ILogger<AuditComparisonService>
{
    private readonly ConcurrentQueue<(LogLevel Level, string Line)> lines = new();

    public IReadOnlyList<(LogLevel Level, string Line)> Lines => [.. lines];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        lines.Enqueue((logLevel, formatter(state, exception)));
}
