using Core.Platform.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Platform.Tests.Logging;

/// <summary>One event becomes one plain line with the fields in a fixed order, whatever the state or the exception look like.</summary>
public sealed class LineConsoleFormatterTests
{
    private const string Marker = "ZQX-MARKER-7731";
    private const string Timestamp = @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z";
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 34, 56, 789, TimeSpan.Zero);
    private static readonly LineConsoleFormatter Formatter = new(new FixedTimeProvider(Now));

    [Fact]
    public void TimestampIsTheClockInstantInUtcWithMilliseconds()
    {
        var entry = Entry(LogLevel.Information, 7, [], exception: null);

        var line = Written(entry);

        Assert.StartsWith("2026-10-16T12:34:56.789Z ", line, StringComparison.Ordinal);
    }

    [Fact]
    public void FieldsComeOutInOrderWithTheTagsAfterTheMessage()
    {
        var entry = Entry(LogLevel.Information, 7, [new("key", "value"), new("{OriginalFormat}", "Mensagem {key}")], exception: null);

        var line = Written(entry);

        Assert.Matches($"^{Timestamp} INFO  Sample\\[7\\] Mensagem value key=value$", line);
    }

    [Fact]
    public void ExceptionChainIsWrittenByTypeAndFramesOnTheSameLine()
    {
        var entry = Entry(LogLevel.Error, 1, [], Thrown());

        var line = Written(entry);

        Assert.DoesNotContain('\n', line);
        Assert.Contains(" exception=System.InvalidOperationException ", line, StringComparison.Ordinal);
        Assert.EndsWith(" <- System.FormatException", line, StringComparison.Ordinal);
        Assert.Contains("LineConsoleFormatterTests.Thrown()", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ExceptionMessagesAreNotWritten()
    {
        var entry = Entry(LogLevel.Error, 1, [], Thrown());

        var line = Written(entry);

        Assert.DoesNotContain(Marker, line, StringComparison.Ordinal);
    }

    [Fact]
    public void LineBreaksInTheMessageAreFlattened()
    {
        var entry = Entry(LogLevel.Warning, 2, [new("{OriginalFormat}", "primeira\r\nsegunda\nterceira")], exception: null);

        var line = Written(entry);

        Assert.EndsWith("WARN  Sample[2] primeira | segunda | terceira", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "TRACE")]
    [InlineData(LogLevel.Debug, "DEBUG")]
    [InlineData(LogLevel.Information, "INFO ")]
    [InlineData(LogLevel.Warning, "WARN ")]
    [InlineData(LogLevel.Error, "ERROR")]
    [InlineData(LogLevel.Critical, "FATAL")]
    public void LevelLabelIsFiveCharactersWide(LogLevel level, string label)
    {
        var entry = Entry(level, 3, [new("{OriginalFormat}", "m")], exception: null);

        var line = Written(entry);

        Assert.Matches($"^{Timestamp} {label} Sample", line);
    }

    private static string Written(LogEntry<SampleTags> entry)
    {
        using var writer = new StringWriter();
        Formatter.Write(entry, scopeProvider: null, writer);
        return writer.ToString().TrimEnd('\r', '\n');
    }

    private static LogEntry<SampleTags> Entry(
        LogLevel level, int eventId, KeyValuePair<string, object?>[] tags, Exception? exception) =>
        new(level, "Sample", new EventId(eventId), new SampleTags(tags), exception, static (state, _) => Message(state));

    private static string Message(SampleTags state) =>
        state.Aggregate(
            state.LastOrDefault(tag => tag.Key == "{OriginalFormat}").Value?.ToString() ?? string.Empty,
            (text, tag) => text.Replace("{" + tag.Key + "}", tag.Value?.ToString(), StringComparison.Ordinal));

    private static InvalidOperationException Thrown()
    {
        try
        {
            throw new InvalidOperationException(Marker, new FormatException(Marker));
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
