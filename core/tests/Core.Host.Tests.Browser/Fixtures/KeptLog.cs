using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>A logger provider that keeps every line the host writes, with its category and its exception, so that a test can look in it for what must never be there.</summary>
internal sealed class KeptLog : ILoggerProvider
{
    private readonly ConcurrentQueue<string> lines = new();

    /// <summary>Every line logged so far, as text.</summary>
    public string Text => string.Join('\n', lines);

    public ILogger CreateLogger(string categoryName) => new Keeping(categoryName, lines);

    public void Dispose()
    {
        // Nothing is held beyond the queue, which goes with the provider.
    }

    private sealed class Keeping(string category, ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            lines.Enqueue($"{category}: {formatter(state, exception)} {exception}");
        }
    }
}
