using Core.Platform.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Core.Platform.Logging;

/// <summary>Writes the console in the same single-line form as the file.</summary>
internal sealed class LineConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "core-line";

    private readonly IClock clock;

    public LineConsoleFormatter(IClock clock)
        : base(FormatterName)
    {
        this.clock = clock;
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        textWriter.WriteLine(LogLine.Format(
            clock.UtcNow, logEntry.LogLevel, logEntry.Category, logEntry.EventId, message, logEntry.Exception));
    }
}
