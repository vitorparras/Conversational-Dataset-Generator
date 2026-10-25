using System.Collections.Concurrent;
using Core.Host.Supervision;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Supervision;

/// <summary>Keeps every line the supervisor writes, formatted as the console would show it, from whichever thread writes it.</summary>
internal sealed class RecordingLogger : ILogger<LanguageServiceSupervisor>
{
    private readonly ConcurrentQueue<string> lines = new();

    public IReadOnlyList<string> Lines => [.. lines];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        lines.Enqueue(formatter(state, exception));
}
