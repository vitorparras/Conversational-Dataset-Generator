using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>A logger provider that keeps what is written to it as the lines a reader would see, to judge what a route writes to the host's log.</summary>
internal sealed class RecordedLog : ILoggerProvider
{
    private readonly ConcurrentQueue<string> lines = new();

    /// <summary>The lines written so far, each with its level and its text.</summary>
    public IReadOnlyList<string> Lines => [.. lines];

    public ILogger CreateLogger(string categoryName) => new Recorder(lines);

    public void Dispose()
    {
        // Nothing is held beyond the queue, which goes with the provider.
    }

    private sealed class Recorder(ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue($"{logLevel}: {formatter(state, exception)}{(exception is null ? string.Empty : " | " + exception)}");
    }
}
