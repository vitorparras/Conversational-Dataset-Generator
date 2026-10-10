using Core.Platform.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Tests.Unit.Platform.Logging;

/// <summary>
/// Issue 34 CA-1, CA-2, CA-4 and CA-5, through the real log path: console, file, and any other
/// destination registered next to them. No other test class touches <see cref="Console.Out"/>, and
/// the tests of one class never run in parallel with each other.
/// </summary>
public sealed partial class RedactionTests : IDisposable
{
    private const string Marker = "ZQX-MARKER-7731";
    private const string ItemId = "item-42";

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cdg-log-");

    public void Dispose() => directory.Delete(recursive: true);

    [Fact]
    public void ClassifiedFieldsAreErasedInEveryDestinationAndTheItemStaysReadable()
    {
        var outputs = Capture(logger =>
            LogClassified(logger, ItemId, Marker + " material", Marker + " identity", Marker + " secret"));

        Assert.All(outputs.All, output =>
        {
            Assert.DoesNotContain(Marker, output, StringComparison.Ordinal);
            Assert.Contains("item " + ItemId + ";", output, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ExceptionDuringProcessingLogsItemStepAndReasonWithoutTheText()
    {
        var outputs = Capture(logger =>
        {
            try
            {
                Process(Marker + " text being processed");
            }
            catch (InvalidOperationException exception)
            {
                logger.ItemFailed(new ItemReference(ItemId, "sanitization", "formato inesperado"), exception);
            }
        });

        Assert.All(outputs.All, output =>
        {
            Assert.DoesNotContain(Marker, output, StringComparison.Ordinal);
            Assert.Contains($"Falha no item {ItemId}, passo sanitization: formato inesperado", output, StringComparison.Ordinal);
        });

        // The platform destinations name the exception type; the message stays out.
        Assert.All(
            [outputs.Console, outputs.File],
            output => Assert.Contains("| exceção: System.InvalidOperationException", output, StringComparison.Ordinal));
    }

    [Fact]
    public void UnclassifiedFieldIsWrittenAsIsSoTheRedactionIsWhatErases()
    {
        // Control for the two tests above: without a classification the value reaches every
        // destination, so its absence there is the redaction's doing and not the destination's.
        var outputs = Capture(logger => LogUnclassified(logger, Marker));

        Assert.All(outputs.All, output => Assert.Contains(Marker, output, StringComparison.Ordinal));
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "item {ItemId}; material {Text}; person {Person}; token {Token}")]
    private static partial void LogClassified(
        ILogger logger, string itemId, [Material] string text, [Identity] string person, [Secret] string token);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "unclassified {Value}")]
    private static partial void LogUnclassified(ILogger logger, string value);

    private static void Process(string text) =>
        throw new InvalidOperationException("could not process: " + text);

    private Outputs Capture(Action<ILogger> write)
    {
        var filePath = Path.Combine(directory.FullName, "core.log");
        using var memory = new MemoryLoggerProvider();
        var originalOut = Console.Out;
        using var console = new StringWriter();
        Console.SetOut(console);
        try
        {
            var services = new ServiceCollection()
                .AddPlatformLogging(LogLevel.Information, filePath)
                .AddSingleton<ILoggerProvider>(memory);
            using (var provider = services.BuildServiceProvider())
            {
                write(provider.GetRequiredService<ILoggerFactory>().CreateLogger("Test"));
            }

            // Disposing the provider drains the console queue and closes the file.
            return new Outputs(console.ToString(), File.ReadAllText(filePath), memory.Text);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private sealed record Outputs(string Console, string File, string Memory)
    {
        public IReadOnlyList<string> All => [Console, File, Memory];
    }

    private sealed class MemoryLoggerProvider : ILoggerProvider, ILogger
    {
        private readonly System.Text.StringBuilder text = new();
        private readonly Lock gate = new();

        public string Text
        {
            get
            {
                lock (gate)
                {
                    return text.ToString();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (gate)
            {
                _ = text.AppendLine(formatter(state, exception));
            }
        }

        public void Dispose()
        {
        }
    }
}
