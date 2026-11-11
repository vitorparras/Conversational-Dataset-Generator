using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Submissions;

/// <summary>Every line the submissions write: its event, its level, the message read by a person, and each field written beside it.</summary>
internal sealed class SubmissionLog : ILogger
{
    private readonly ConcurrentQueue<(int EventId, LogLevel Level, string Line)> lines = new();

    public IReadOnlyList<(int EventId, LogLevel Level, string Line)> Lines => [.. lines];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var fields = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
        var written = string.Join(' ', fields.Select(field => $"{field.Key}={Convert.ToString(field.Value, CultureInfo.InvariantCulture)}"));
        lines.Enqueue((eventId.Id, logLevel, $"{formatter(state, exception)} {written}"));
    }
}
