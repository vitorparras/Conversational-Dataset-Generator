using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Core.Boundary.Contracts;
using Core.Boundary.Http;
using Core.Host.Analysis;
using Core.Host.Supervision;
using Core.Host.Tests.Supervision;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests.Analysis;

/// <summary>
/// Whoever holds the port of the language service without holding its secret gets no byte of the secret and no byte of the text,
/// whether it proves nothing, guesses the proof or replays one it was once given; and every connection asks for the proof of a nonce
/// of its own (issue #402, criteria 2 and 4).
/// </summary>
public sealed class StrangerProofTests : IDisposable
{
    private const string Marker = "ZQX-MARKER-0402-TEXT";
    private const string PlainAnswer = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}";
    private const string VersionBody = /*lang=json,strict*/ """{"service_version":"0.1.0","contract_version":"1","model_version":null}""";
    private const string SecretHeader = "x-service-secret";

    private static readonly string VersionAnswer =
        string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {VersionBody.Length}\r\n\r\n{VersionBody}");

    private static readonly AnalysisAsked Asked = new($"falei com PESSOA_1 ontem {Marker}", "pt");

    private readonly SupervisionHarness harness = new();
    private readonly string secret = SupervisionHarness.NewSecret();
    private readonly List<HttpClient> clients = [];

    public void Dispose()
    {
        clients.ForEach(client => client.Dispose());
        harness.Dispose();
    }

    [Theory]
    [InlineData("Silent")]
    [InlineData("Guessing")]
    public async Task StrangerThatDoesNotProveIsUnreachableAndGetsNeitherSecretNorText(string kind)
    {
        using var stranger = new RecordingStranger(0, (_, chunk, request) => chunk > 0 ? null : Unproven(kind, request));

        var result = await AnalyzeAsync(stranger.Port, NewClient());
        await EndedAsync(stranger);

        Assert.Equal((UnavailabilityReason.ConnectionRefused, 0), (result.Refused!.Reason, Leaks(stranger.ReceivedAll)));
        Assert.Null(result.Concluded);
        Assert.StartsWith("GET /version", stranger.ReceivedOn(0), StringComparison.Ordinal);
        _ = Assert.Single(stranger.Nonces);
    }

    [Fact]
    public async Task StrangerThatFailedTheProofGetsNothingOnTheNextAnalysisEither()
    {
        using var stranger = new RecordingStranger(0, (_, chunk, _) => chunk > 0 ? null : PlainAnswer);
        var client = NewClient();

        var first = await AnalyzeAsync(stranger.Port, client);
        var second = await AnalyzeAsync(stranger.Port, client);
        await EndedAsync(stranger);

        Assert.Equal(
            (UnavailabilityReason.ConnectionRefused, UnavailabilityReason.ConnectionRefused, 0),
            (first.Refused!.Reason, second.Refused!.Reason, Leaks(stranger.ReceivedAll)));
    }

    [Fact]
    public async Task StrangerThatReplaysAProofItWasGivenGetsNothingOnTheNextConnection()
    {
        string? replayed = null;
        string? Holder(string request)
        {
            replayed = ProofAnswer.To(request, secret);
            return replayed;
        }

        using var stranger = new RecordingStranger(0, (connection, chunk, request) => (connection, chunk) switch
        {
            (0, 0) => Holder(request),
            (0, _) => Analyzed(request),
            (_, 0) => replayed,
            _ => null,
        });

        _ = await AnalyzeAsync(stranger.Port, NewClient());
        var replay = await AnalyzeAsync(stranger.Port, NewClient());
        await EndedAsync(stranger);

        Assert.Equal((UnavailabilityReason.ConnectionRefused, 0), (replay.Refused!.Reason, Leaks(stranger.ReceivedOn(1))));
    }

    [Fact]
    public async Task ProcessThatProvesTheSecretReceivesTheTextAndTheSecretOnlyAfterTheProofRequest()
    {
        using var stranger = new RecordingStranger(0, (_, chunk, request) => chunk == 0 ? ProofAnswer.To(request, secret) : Analyzed(request));

        _ = await AnalyzeAsync(stranger.Port, NewClient());
        await EndedAsync(stranger);

        var received = stranger.ReceivedOn(0);
        var proofRequest = received[..received.IndexOf("\r\n\r\n", StringComparison.Ordinal)];
        Assert.Equal(
            (true, true, 0),
            (received.Contains(secret, StringComparison.Ordinal), received.Contains(Marker, StringComparison.Ordinal), Leaks(proofRequest)));
    }

    [Fact]
    public async Task EveryConnectionAsksForTheProofOfANonceOfItsOwn()
    {
        const int Connections = 6;
        using var stranger = new RecordingStranger(0, (_, chunk, request) => chunk == 0 ? ProofAnswer.To(request, secret) : Analyzed(request));

        foreach (var attempt in Enumerable.Range(0, Connections))
        {
            _ = await AnalyzeAsync(stranger.Port, NewClient());
            Assert.Equal(attempt + 1, stranger.Connections);
        }

        await EndedAsync(stranger);
        var nonces = stranger.Nonces;
        Assert.Equal((Connections, Connections), (nonces.Count, nonces.Distinct().Count()));
        Assert.All(nonces, nonce => Assert.Matches("^[0-9a-f]{64}$", nonce));
    }

    [Trait("Category", "RealService")]
    [Theory]
    [InlineData("Silent")]
    [InlineData("Guessing")]
    public async Task StrangerOnThePortOfAServiceThatDiedGetsNeitherTheSecretItWasStartedWithNorTheText(string kind)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var watch = new ServiceProcessWatch();
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        var endpoint = supervisor.Endpoint!;
        StandInService.Kill(supervisor.ProcessId!.Value);
        var lost = await Poll.UntilAsync(() => Task.FromResult(watch.State == ServiceState.Unavailable), Poll.LossLimit, cancellationToken);
        Assert.True(lost, "the end of the service process was not seen");
        Assert.True(await PortReleased.UntilAsync(endpoint.Port, cancellationToken), "the port of the killed service was never let go");
        using var stranger = new RecordingStranger(endpoint.Port, (_, chunk, request) => chunk > 0 ? null : Unproven(kind, request));

        var result = await AnalyzeAsync(endpoint.Port, NewClient(), endpoint.Secret);
        await EndedAsync(stranger);

        Assert.Equal((UnavailabilityReason.ConnectionRefused, 0), (result.Refused!.Reason, LeaksOf(stranger.ReceivedAll, endpoint.Secret)));
        Assert.True(stranger.Connections > 0, "the stranger was never asked for the proof, so the test did not reach the connection of the core");
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task RealServiceAnswersEachNonceWithTheHmacOfThatNonceAndTwoNoncesGiveTwoProofs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(new ServiceProcessWatch(), cancellationToken);
        var endpoint = supervisor.Endpoint!;
        var first = new string('a', 64);
        var second = new string('b', 64);

        var firstProof = await ProofOfAsync(endpoint.Port, first, cancellationToken);
        var secondProof = await ProofOfAsync(endpoint.Port, second, cancellationToken);

        Assert.Equal((ExpectedProof(endpoint.Secret, first), ExpectedProof(endpoint.Secret, second)), (firstProof, secondProof));
        Assert.NotEqual(firstProof, secondProof);
    }

    /// <summary>The answers of a holder: the version when asked, and the analysis once the text has arrived, which may come a chunk after the head.</summary>
    private static string? Analyzed(string chunk)
    {
        if (chunk.StartsWith("GET /version", StringComparison.Ordinal))
        {
            return VersionAnswer;
        }

        return chunk.Contains(Marker, StringComparison.Ordinal) ? PlainAnswer : null;
    }

    /// <summary>What a process that does not hold the secret answers to the request for the proof: nothing of a proof, or a guess at one.</summary>
    private static string? Unproven(string kind, string request) => kind == "Silent" ? PlainAnswer : ProofAnswer.To(request, "not-the-secret");

    /// <summary>How many of the three things the stranger must never see are in what it received: the secret, the text, the name of the header of the secret.</summary>
    private int Leaks(string received) => LeaksOf(received, secret);

    private static int LeaksOf(string received, string held) =>
        new[] { held, Marker }.Count(item => received.Contains(item, StringComparison.Ordinal))
        + (received.Contains(SecretHeader, StringComparison.OrdinalIgnoreCase) ? 1 : 0);

    /// <summary>The clients are let go first, since a connection a client keeps for the next request does not end before it.</summary>
    private Task EndedAsync(RecordingStranger stranger)
    {
        clients.ForEach(client => client.Dispose());
        return stranger.AllEndedAsync();
    }

    private HttpClient NewClient()
    {
        var client = LoopbackHttpClient.Create();
        clients.Add(client);
        return client;
    }

    private Task<AnalysisResult> AnalyzeAsync(int port, HttpClient client) => AnalyzeAsync(port, client, secret);

    private static Task<AnalysisResult> AnalyzeAsync(int port, HttpClient client, string held)
    {
        var settings = new HttpRecognizerSettings(port, held, SupervisionHarness.Timeout);
        var recognizer = new HttpRecognizer(client, settings, NullLogger<HttpRecognizer>.Instance);
        return new MaterialAnalysis(recognizer, new AnalysisLog()).AnalyzeAsync(Asked, TestContext.Current.CancellationToken);
    }

    /// <summary>What the real service answers, in the proof header, to a request with a nonce and no secret.</summary>
    private static async Task<string> ProofOfAsync(int port, string nonce, CancellationToken cancellationToken)
    {
        using var handler = new SocketsHttpHandler { UseProxy = false };
        using var plain = new HttpClient(handler, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"http://127.0.0.1:{port}/version"));
        request.Headers.Add("x-service-nonce", nonce);
        using var response = await plain.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.Single(response.Headers.GetValues("x-service-proof"));
    }

    private static string ExpectedProof(string held, string nonce) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(held), Encoding.ASCII.GetBytes(nonce)));
}
