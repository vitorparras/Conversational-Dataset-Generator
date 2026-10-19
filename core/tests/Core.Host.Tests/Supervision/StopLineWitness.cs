using Core.Host.Supervision;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Supervision;

/// <summary>A logger that, when the stop line arrives, looks at the process the line names and notes whether it still runs.</summary>
internal sealed class StopLineWitness : ILogger<LanguageServiceSupervisor>
{
    private const int StopEvent = 110;

    /// <summary>Whether the service still ran when its stop line was written; null until the line arrives.</summary>
    public bool? ServiceRunningAtStopLine { get; private set; }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (eventId.Id == StopEvent && state is IReadOnlyList<KeyValuePair<string, object?>> fields)
        {
            ServiceRunningAtStopLine = ProcessTree.IsRunning((int)fields.First(field => field.Key == "ProcessId").Value!);
        }
    }
}
