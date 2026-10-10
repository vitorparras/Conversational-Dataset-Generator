using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Core.Platform.Logging;

/// <summary>
/// The single line every destination writes. An exception contributes its type only: its message
/// is free text written by whoever threw it, and may carry the content being processed.
/// </summary>
internal static class LogLine
{
    public static string Format(
        DateTimeOffset timestamp,
        LogLevel level,
        string loggerName,
        EventId eventId,
        string message,
        Exception? exception)
    {
        var line = new StringBuilder()
            .Append(timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(LevelName(level))
            .Append(' ')
            .Append(loggerName)
            .Append('[')
            .Append(eventId.Id.ToString(CultureInfo.InvariantCulture))
            .Append("] ")
            .Append(message.ReplaceLineEndings(" "));

        for (var current = exception; current is not null; current = current.InnerException)
        {
            _ = line.Append(current == exception ? " | exceção: " : " <- ").Append(current.GetType().FullName);
        }

        return line.ToString();
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "fail",
        LogLevel.Critical => "crit",
        LogLevel.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}
