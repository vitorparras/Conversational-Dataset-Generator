using Core.Boundary.Contracts;
using Core.Boundary.Http;
using Core.Host.Analysis;
using Core.Host.Tests.Analysis;
using Core.Host.Tests.Logging;
using Core.Host.Tests.Supervision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Privacy;

/// <summary>
/// A language service that dies with the request in flight: the analysis, written through the logging the host composes
/// to the console and to the file, leaves the marker in neither, and its refusal is logged (issue #251, CA-5). The error
/// stream is not captured in process; the one run that sweeps it is the killed service of KilledServiceLogTests.
/// </summary>
[Collection("console")]
public sealed class InFlightDeathLogTests
{
    [Fact]
    public async Task ServiceDyingWithTheRequestInFlightLeavesTheMarkerInNoDestination()
    {
        var marker = LogMarker.Fresh();
        using var beginning = new SweepMarker();
        using var composed = new ComposedFile();
        using var service = new DyingService();
        using var harness = new SupervisionHarness();
        AnalysisResult? result = null;

        var file = await composed.TextWrittenByAsync(async services =>
        {
            var settings = new HttpRecognizerSettings(service.Port, SupervisionHarness.NewSecret(), SupervisionHarness.Timeout);
            var recognizer = new HttpRecognizer(harness.Client, settings, services.GetRequiredService<ILogger<HttpRecognizer>>());
            var analysis = new MaterialAnalysis(recognizer, services.GetRequiredService<ILogger<MaterialAnalysis>>());
            result = await analysis.AnalyzeAsync(new AnalysisAsked($"falei com PESSOA_1 {marker}", "pt"), TestContext.Current.CancellationToken);
        });

        var leaks = LogSweep.Leaks(new LogEvidence(marker, composed.ConsoleText, string.Empty, composed.Path, beginning.Since));
        Assert.Equal(UnavailabilityReason.MalformedResponse, result!.Refused!.Reason);
        Assert.Contains($"[204] Análise {result.Refused.Reference} interrompida: MalformedResponse", file, StringComparison.Ordinal);
        Assert.Empty(leaks);
    }
}
