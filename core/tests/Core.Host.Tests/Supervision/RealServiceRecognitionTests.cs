using Core.Boundary.Contracts;
using Core.Host.Supervision;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests.Supervision;

/// <summary>
/// A rehearsal apart from the suite: the recognizer the composition root registers for the kind http analyses a text
/// against the real language service. Without the service environment it is skipped with the reason, never failed.
/// </summary>
[Trait("Category", "RealService")]
public sealed class RealServiceRecognitionTests : IDisposable
{
    private static readonly AnalysisRequest Request =
        new("falei com PESSOA_1 ontem ZQX-MARKER-TEXT-0026", "pt", "ZQX-REF-0026", ContractVersion.Current);

    private readonly SupervisionHarness harness = new();

    [Fact]
    public async Task RecognizerResolvedForHttpAnalysesATextAgainstTheRealService()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var interpreter = LanguageServiceInterpreter.Available;
        Assert.SkipWhen(interpreter is null, "Real service rehearsal skipped: uv could not resolve the language-service environment.");
        var supervisor = harness.SupervisorFor(interpreter);
        var started = await supervisor.StartAsync(SupervisionHarness.Timeout, cancellationToken);
        Assert.SkipUnless(started == StartOutcome.Started, $"Real service rehearsal skipped: the language service did not start ({started}).");
        using var container = RecognizerContainer.Of(RecognizerContainer.Loaded("--Recognizer:Kind=http").Values, supervisor, harness.Client);

        var outcome = await container.GetRequiredService<IRecognizer>().RecognizeAsync(Request, cancellationToken);

        Assert.Null(outcome.Reason);
        Assert.Empty(outcome.Response!.Occurrences);
    }

    public void Dispose() => harness.Dispose();
}
