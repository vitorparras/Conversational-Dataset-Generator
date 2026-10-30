using System.Collections.Concurrent;
using System.Globalization;
using Core.Boundary.Contracts;
using Core.Host.Analysis;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Analysis;

/// <summary>
/// The recognizer of a service that is not running refuses the request as out of reach, writes one line naming only the
/// reference of the request, and never the text (requirement 021; issue #404).
/// </summary>
public sealed class StoppedServiceRecognizerTests
{
    private const string Marker = "ZQX-MARKER-0404-STOPPED";

    private readonly StoppedLog log = new();

    [Fact]
    public async Task RefusalIsConnectionRefusedWithNoAnalysis()
    {
        var outcome = await new StoppedServiceRecognizer(log).RecognizeAsync(RequestOf("ref-0404-a"), TestContext.Current.CancellationToken);

        Assert.Equal(RecognitionOutcome.Unavailable(UnavailabilityReason.ConnectionRefused), outcome);
        Assert.Null(outcome.Response);
        Assert.Equal(UnavailabilityReason.ConnectionRefused, outcome.Reason);
    }

    [Fact]
    public async Task RefusalIsWrittenOnceAsEvent206AtErrorLevel()
    {
        _ = await new StoppedServiceRecognizer(log).RecognizeAsync(RequestOf("ref-0404-b"), TestContext.Current.CancellationToken);

        var written = Assert.Single(log.Entries);
        Assert.Equal(206, written.EventId);
        Assert.Equal(LogLevel.Error, written.Level);
        Assert.Null(written.Exception);
    }

    [Fact]
    public async Task RefusalLineNamesTheReferenceOfTheRequest()
    {
        _ = await new StoppedServiceRecognizer(log).RecognizeAsync(RequestOf("ref-0404-c"), TestContext.Current.CancellationToken);

        var written = Assert.Single(log.Entries);
        Assert.Equal("ref-0404-c", written.Fields["Reference"]);
        Assert.Contains("ref-0404-c", written.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusalLineSaysTheCauseIsTheServiceNotRunningAndNothingWasSent()
    {
        _ = await new StoppedServiceRecognizer(log).RecognizeAsync(RequestOf("ref-0404-e"), TestContext.Current.CancellationToken);

        var written = Assert.Single(log.Entries);
        Assert.Contains("o serviço de linguagem não está em execução", written.Message, StringComparison.Ordinal);
        Assert.Contains("recusada sem enviar nada", written.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusalLineNeverCarriesTheTextOfTheRequest()
    {
        _ = await new StoppedServiceRecognizer(log).RecognizeAsync(RequestOf("ref-0404-d"), TestContext.Current.CancellationToken);

        var written = Assert.Single(log.Entries);
        Assert.DoesNotContain(Marker, written.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(written.Fields.Values, value => Convert.ToString(value, CultureInfo.InvariantCulture)?.Contains(Marker, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task NullRequestThrowsAndWritesNothing()
    {
        var recognizer = new StoppedServiceRecognizer(log);

        var thrown = await Assert.ThrowsAsync<ArgumentNullException>(
            () => recognizer.RecognizeAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal("request", thrown.ParamName);
        Assert.Empty(log.Entries);
    }

    private static AnalysisRequest RequestOf(string reference) => new($"falei com PESSOA_1 ontem {Marker}", "pt", reference, ContractVersion.Current);

    /// <summary>Every line written to the recognizer's logger: its event, level, message and each field written beside it.</summary>
    private sealed class StoppedLog : ILogger<StoppedServiceRecognizer>
    {
        private readonly ConcurrentQueue<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => [.. entries];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var fields = (state as IEnumerable<KeyValuePair<string, object?>> ?? [])
                .ToDictionary(field => field.Key, field => field.Value);
            entries.Enqueue(new Entry(eventId.Id, logLevel, formatter(state, exception), exception, fields));
        }

        internal sealed record Entry(int EventId, LogLevel Level, string Message, Exception? Exception, IReadOnlyDictionary<string, object?> Fields);
    }
}
