using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Core.Persistence.Tests.People;

/// <summary>Every line the driver writes at any level: the message read by a person, each field written beside it and the text of the exception, as one text to search.</summary>
internal sealed class CapturedNpgsqlLog : ILoggerFactory
{
    private readonly ConcurrentQueue<string> lines = new();

    /// <summary>Everything written so far.</summary>
    public string Everything => string.Join('\n', lines);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => new Writer(lines);

    public void Dispose()
    {
    }

    private sealed class Writer : ILogger
    {
        private readonly ConcurrentQueue<string> lines;

        internal Writer(ConcurrentQueue<string> lines)
        {
            this.lines = lines;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var fields = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
            var written = string.Join(' ', fields.Select(field => $"{field.Key}={Convert.ToString(field.Value, CultureInfo.InvariantCulture)}"));
            lines.Enqueue($"{logLevel} {eventId.Id} {formatter(state, exception)} {written} {exception}");
        }
    }
}
