using System.Collections.Concurrent;
using Core.Boundary.Http;
using Microsoft.Extensions.Logging;

namespace Core.Boundary.Tests.Http;

/// <summary>Every line the recognizer writes, as the message read by a person plus each field written beside it.</summary>
internal sealed class CapturedLog : ILogger<HttpRecognizer>
{
    private readonly ConcurrentQueue<CapturedLine> lines = new();

    public IReadOnlyList<CapturedLine> Lines => [.. lines];

    /// <summary>Everything written, messages and field values, as one text to search.</summary>
    public string Everything => string.Join('\n', Lines.Select(line => line.ToString()));

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var fields = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
        lines.Enqueue(new CapturedLine(
            logLevel, eventId.Id, formatter(state, exception), fields.ToDictionary(field => field.Key, field => Convert.ToString(field.Value, System.Globalization.CultureInfo.InvariantCulture))));
    }
}
