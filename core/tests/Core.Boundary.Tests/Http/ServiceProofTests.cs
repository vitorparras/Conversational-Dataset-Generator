using Core.Boundary.Contracts;
using Core.Boundary.Http;
using Core.Platform.Versions;

namespace Core.Boundary.Tests.Http;

/// <summary>No byte of secret or text goes to a port before the process on it has proved, with the HMAC of a new nonce, that it holds the secret.</summary>
public sealed class ServiceProofTests : IDisposable
{
    private static readonly TimeSpan Generous = TimeSpan.FromMinutes(5);

    private readonly Recognition recognition = new();

    public void Dispose() => recognition.Dispose();

    [Fact]
    public async Task ServiceThatProvesIsAskedWithoutTheSecretAndThenAnalyzes()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/response-empty.json"));

        var outcome = await recognition.AskAsync(service.Port);

        Assert.NotNull(outcome.Response);
        Assert.Null(Assert.Single(service.ProofSecrets));
    }

    [Theory]
    [InlineData("Wrong")]
    [InlineData("Missing")]
    public async Task ServiceThatDoesNotProveIsUnreachableAndGetsNeitherSecretNorText(string mode)
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/response-empty.json"));
        service.Proof = Enum.Parse<ProofMode>(mode);

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal((UnavailabilityReason.ConnectionRefused, 0), (outcome.Reason, service.Seen.Count));
    }

    [Theory]
    [InlineData("Wrong")]
    [InlineData("Missing")]
    public async Task ServiceThatDoesNotProveHasNoVersionsAndGetsNoSecret(string mode)
    {
        using var service = FakeService.Answering(Reply.Version("1"), Reply.Ok("{}"));
        service.Proof = Enum.Parse<ProofMode>(mode);
        using var client = LoopbackHttpClient.Create();
        var reader = new HttpServiceVersionReader(client, new HttpRecognizerSettings(service.Port, Reply.Secret, Generous));

        var versions = await reader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal((ServiceVersions.Absent, 0), (versions, service.Seen.Count));
    }

    [Fact]
    public async Task EachConnectionAsksForANewNonceAndTheProofsDiffer()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/response-empty.json"));
        using var other = new Recognition();

        _ = await recognition.AskAsync(service.Port);
        _ = await other.AskAsync(service.Port);

        var nonces = service.Nonces;
        Assert.Equal(
            (2, 2, 64),
            (nonces.Count, nonces.Distinct().Count(), nonces[0].Length));
    }

    [Theory]
    [InlineData("HTTP/1.1 401 Unauthorized", true, "Content-Length: 2")]
    [InlineData("HTTP/1.1 200 OK", true, "Transfer-Encoding: chunked")]
    [InlineData("HTTP/1.1 200 OK", true, "Content-Length: 999999")]
    [InlineData("HTTP/1.1 200 OK", true, "Content-Length: -1")]
    [InlineData("HTTP/1.1 200 OK", false, "Content-Length: 2")]
    public async Task AnswerThatIsNotAWholeSuccessWithTheRightProofIsUnreachable(string status, bool withProof, string framing)
    {
        using var service = new RawService(
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n{}",
            firstAnswer: nonce => $"{status}\r\n{(withProof ? $"{TestProof.ProofHeader}: {TestProof.Of(Reply.Secret, nonce)}\r\n" : string.Empty)}{framing}\r\n\r\n{{}}");

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.ConnectionRefused, outcome.Reason);
    }

    [Fact]
    public async Task AnswerWhoseHeadNeverEndsIsUnreachable()
    {
        using var service = new RawService(
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n{}",
            firstAnswer: _ => "HTTP/1.1 200 OK\r\n" + string.Concat(Enumerable.Repeat("x-padding: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\r\n", 400)));

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.ConnectionRefused, outcome.Reason);
    }

    [Fact]
    public async Task RequestWithNoSecretFindsNoConnectionAndSendsNothing()
    {
        using var service = FakeService.Answering(Reply.Version("1"), Reply.Ok("{}"));
        using var client = LoopbackHttpClient.Create();

        var refused = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync(new Uri($"http://127.0.0.1:{service.Port}/version"), TestContext.Current.CancellationToken));

        Assert.Equal((HttpRequestError.ConnectionError, 0), (refused.HttpRequestError, service.Seen.Count + service.Nonces.Count));
    }
}
