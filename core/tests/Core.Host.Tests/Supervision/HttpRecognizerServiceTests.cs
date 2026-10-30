using Core.Boundary.Contracts;
using Core.Boundary.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests.Supervision;

/// <summary>
/// The recognizer against the real language service, started the way the supervisor starts it: the service reads the
/// secret from its standard input, declares contract version 1 on its version route, and today finds nothing.
/// </summary>
[Trait("Category", "RealService")]
public sealed class HttpRecognizerServiceTests : IDisposable
{
    private static readonly AnalysisRequest Request =
        new("falei com PESSOA_1 ontem ZQX-MARKER-TEXT-0025", "pt", "ZQX-REF-0025", ContractVersion.Current);

    private readonly SupervisionHarness harness = new();

    [Fact]
    public async Task RealServiceWithItsSecretAnalysesTheTextAndFindsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var secret = SupervisionHarness.NewSecret();
        var service = await harness.StartHealthyAsync(secret, cancellationToken);
        var recognizer = RecognizerOf(service.Port, secret);

        var outcome = await recognizer.RecognizeAsync(Request, cancellationToken);

        Assert.Empty(outcome.Response!.Occurrences);
        Assert.Null(outcome.Response.ModelVersion);
    }

    [Fact]
    public async Task RealServiceGivenAnotherSecretLeavesTheTextUnanalysed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = await harness.StartHealthyAsync(SupervisionHarness.NewSecret(), cancellationToken);
        var recognizer = RecognizerOf(service.Port, SupervisionHarness.NewSecret());

        var outcome = await recognizer.RecognizeAsync(Request, cancellationToken);

        Assert.Equal(UnavailabilityReason.MalformedResponse, outcome.Reason);
    }

    public void Dispose() => harness.Dispose();

    private HttpRecognizer RecognizerOf(int port, string secret) =>
        new(harness.Client, new HttpRecognizerSettings(port, secret, SupervisionHarness.Timeout), NullLogger<HttpRecognizer>.Instance);
}
