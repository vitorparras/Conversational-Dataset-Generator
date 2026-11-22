using System.Collections.Concurrent;
using System.Globalization;
using Core.Host.Analysis;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Analysis;

/// <summary>Every line the analysis writes: its event, the message read by a person, and each field written beside it.</summary>
internal sealed class AnalysisLog : ILogger<MaterialAnalysis>
{
    private readonly ConcurrentQueue<(int EventId, string Line)> lines = new();

    public IReadOnlyList<string> Lines => [.. lines.Select(line => line.Line)];

    /// <summary>The lines of one event.</summary>
    public IReadOnlyList<string> Of(int eventId) => [.. lines.Where(line => line.EventId == eventId).Select(line => line.Line)];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var fields = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
        var written = string.Join(' ', fields.Select(field => $"{field.Key}={Convert.ToString(field.Value, CultureInfo.InvariantCulture)}"));
        lines.Enqueue((eventId.Id, $"{formatter(state, exception)} {written}"));
    }
}
