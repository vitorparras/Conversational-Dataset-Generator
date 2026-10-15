using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Core.Platform.Logging;

/// <summary>Writes each event as one plain line: timestamp, level, category, event, message, tags, then the exception's type and frames.</summary>
public sealed class LineConsoleFormatter : ConsoleFormatter
{
    /// <summary>The name the console provider knows this formatter by.</summary>
    public const string FormatterName = "line";

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    private const string OriginalFormatKey = "{OriginalFormat}";
    private const string LineBreakReplacement = " | ";
    private const string InnerExceptionSeparator = " <- ";

    /// <summary>A formatter with nothing to configure.</summary>
    public LineConsoleFormatter()
        : base(FormatterName)
    {
    }

    /// <inheritdoc />
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        var line = Head(logEntry) + Tags(logEntry.State) + Describe(logEntry.Exception);
        textWriter.WriteLine(OneLine(line));
    }

    private static string Head<TState>(in LogEntry<TState> entry)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var message = entry.Formatter(entry.State, entry.Exception);
        return string.Create(
            CultureInfo.InvariantCulture, $"{timestamp} {Label(entry.LogLevel)} {entry.Category}[{entry.EventId.Id}] {message}");
    }

    private static string Tags<TState>(TState state) =>
        state is IReadOnlyList<KeyValuePair<string, object?>> tags
            ? string.Concat(tags.Where(tag => tag.Key != OriginalFormatKey).Select(Tag))
            : string.Empty;

    private static string Tag(KeyValuePair<string, object?> tag) =>
        string.Create(CultureInfo.InvariantCulture, $" {tag.Key}={Convert.ToString(tag.Value, CultureInfo.InvariantCulture)}");

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
        string.Join(LineBreakReplacement, text.ReplaceLineEndings("\n").Split('\n').Select(part => part.Trim()));

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
