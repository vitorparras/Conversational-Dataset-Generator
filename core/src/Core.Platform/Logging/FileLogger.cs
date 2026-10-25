using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Core.Platform.Logging;

/// <summary>Formats each event of one category with the console's formatter and hands the line to the file.</summary>
internal sealed class FileLogger : ILogger
{
    private readonly string category;
    private readonly ConsoleFormatter formatter;
    private readonly Action<string> append;

    internal FileLogger(string category, ConsoleFormatter formatter, Action<string> append)
    {
        this.category = category;
        this.formatter = formatter;
        this.append = append;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <summary>The signature is the interface's, so it is implemented explicitly.</summary>
    void ILogger.Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        using var line = new StringWriter(CultureInfo.InvariantCulture);
        this.formatter.Write(new LogEntry<TState>(logLevel, category, eventId, state, exception, formatter), scopeProvider: null, line);
        append(line.ToString());
    }
}
