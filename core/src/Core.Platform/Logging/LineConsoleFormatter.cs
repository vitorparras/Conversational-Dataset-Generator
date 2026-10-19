using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Core.Platform.Logging;

/// <summary>Writes each event as one plain line: timestamp, level, category, event, message, tags, then the exception's type and frames.</summary>
public sealed class LineConsoleFormatter : ConsoleFormatter
{
    /// <summary>The name the console provider knows this formatter by.</summary>
    public static string FormatterName { get; } = "line";

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    private const string OriginalFormatKey = "{OriginalFormat}";
    private const string LineBreakReplacement = " | ";
    private const string InnerExceptionSeparator = " <- ";

    private readonly TimeProvider clock;

    /// <summary>A formatter that stamps each line with the time its clock gives.</summary>
    public LineConsoleFormatter(TimeProvider clock)
        : base(FormatterName)
    {
        this.clock = clock;
    }

    /// <inheritdoc />
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        var line = Head(logEntry) + Tags(logEntry.State) + Describe(logEntry.Exception);
        textWriter.WriteLine(OneLine(line));
    }

    private string Head<TState>(in LogEntry<TState> entry)
    {
        var timestamp = clock.GetUtcNow().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var message = entry.Formatter(entry.State, entry.Exception);
        return string.Create(
            CultureInfo.InvariantCulture, $"{timestamp} {Label(entry.LogLevel)} {entry.Category}[{entry.EventId.Id}] {message}");
    }

    private static string Tags<TState>(TState state) =>
        state is IReadOnlyList<KeyValuePair<string, object?>> tags
            ? string.Concat(tags.Where(tag => tag.Key != OriginalFormatKey).Select(Tag))
            : string.Empty;

    private static string Tag(KeyValuePair<string, object?> pair) =>
        string.Create(CultureInfo.InvariantCulture, $" {pair.Key}={Convert.ToString(pair.Value, CultureInfo.InvariantCulture)}");

    /// <summary>The exception chain by type and frames only; its messages are text the platform never classified.</summary>
    private static string Describe(Exception? exception) =>
        exception is null
            ? string.Empty
            : " exception=" + string.Join(InnerExceptionSeparator, Chain(exception).Select(TypeAndFrames));

    private static string TypeAndFrames(Exception exception) =>
        exception.StackTrace is { } frames ? $"{exception.GetType().FullName} {frames}" : exception.GetType().FullName!;

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static string OneLine(string text) =>
        string.Join(LineBreakReplacement, text.ReplaceLineEndings(Environment.NewLine).Split(Environment.NewLine).Select(part => part.Trim()));

    private static string Label(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "FATAL",
        LogLevel.None => "NONE ",
        _ => level.ToString(),
    };
}
