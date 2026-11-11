using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>Keeps every line the periodic comparison writes, with its level, formatted as the console would show it, from whichever thread writes it.</summary>
internal sealed class AuditComparisonLog : ILogger<AuditComparisonService>
{
    private readonly ConcurrentQueue<(LogLevel Level, string Line)> lines = new();
    private readonly ConcurrentQueue<AuditComparisonStatus> published = new();
    private AuditComparisonState? watched;

    public IReadOnlyList<(LogLevel Level, string Line)> Lines => [.. lines];

    /// <summary>The status that was published when each line was written, in the order of the lines: a line written before its round is published sees the round before.</summary>
    public IReadOnlyList<AuditComparisonStatus> StatusWhenWritten => [.. published];

    /// <summary>Makes the log note, with each line, the status that the state publishes at that moment.</summary>
    /// <param name="state">The state the service publishes to.</param>
    public void Watch(AuditComparisonState state) => watched = state;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        published.Enqueue(watched?.Current ?? AuditComparisonStatus.Pending);
        lines.Enqueue((logLevel, formatter(state, exception)));
    }
}
