using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>Keeps every line a host logs, with the id of its event in brackets, so that a test can tie what a page said to the line of the log.</summary>
internal sealed class HostLog : ILoggerProvider
{
    private readonly ConcurrentQueue<string> lines = new();

    /// <summary>Every line logged so far, as <c>category[event id]: message</c>.</summary>
    public IReadOnlyList<string> Lines => [.. lines];

    public ILogger CreateLogger(string categoryName) => new Keeping(categoryName, lines);

    public void Dispose()
    {
    }

    private sealed class Keeping(string category, ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            lines.Enqueue($"{category}[{eventId.Id}]: {formatter(state, exception)}");
        }
    }
}
