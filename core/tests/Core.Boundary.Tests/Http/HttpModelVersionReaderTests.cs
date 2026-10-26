using Core.Boundary.Http;
using Core.Platform.Versions;

namespace Core.Boundary.Tests.Http;

/// <summary>
/// The model version is whatever the service says it is at the moment of the read, and nothing otherwise: any failure,
/// an answer outside the shape of a version, or a model declared null or blank is an absent version, never an empty
/// one and never one remembered from before. The service is a stand-in on a free loopback port.
/// </summary>
public sealed class HttpModelVersionReaderTests : IDisposable
{
    private const int Hang = 60_000;

    private static readonly TimeSpan Generous = TimeSpan.FromMinutes(5);

    private readonly HttpClient client = LoopbackHttpClient.Create();

    [Fact]
    public async Task ModelTheServiceNamesIsThePresentVersion()
    {
        using var service = FakeService.Answering(Reply.VersionOfModel("pt-model-3.2"), Reply.Ok("{}"));

        var version = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Present("pt-model-3.2"), version);
    }

    [Fact]
    public async Task PortWithNoListenerIsAnAbsentVersion()
    {
        var version = await Reader(FakeService.FreePort(), Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Absent, version);
    }

    [Fact]
    public async Task SecretTheServiceRefusesIsAnAbsentVersion()
    {
        using var service = FakeService.Answering(Reply.Unauthorized, Reply.Ok("{}"));

        var version = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Absent, version);
    }

    [Fact]
    public async Task RedirectIsNotFollowedAndIsAnAbsentVersion()
    {
        using var service = FakeService.Answering(
            Reply.VersionOfModel("pt-model-3.2") with { Status = 302, Location = "http://127.0.0.1:1/version" }, Reply.Ok("{}"));

        var version = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Absent, version);
    }

    [Fact(Timeout = Hang)]
    public async Task ServiceThatAnswersAfterTheLimitIsAnAbsentVersion()
    {
        using var service = FakeService.Answering(
            Reply.VersionOfModel("pt-model-3.2") with { Delay = TimeSpan.FromSeconds(5) }, Reply.Ok("{}"));

        var version = await Reader(service.Port, TimeSpan.FromMilliseconds(200)).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Absent, version);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData(/*lang=json,strict*/ """{"contract_version":"1","model_version":"pt-model-3.2"}""")]
    [InlineData(/*lang=json,strict*/ """{"service_version":"0.1.0","contract_version":"1","model_version":"pt-model-3.2","extra":1}""")]
    [InlineData(/*lang=json,strict*/ """{"service_version":"0.1.0","contract_version":"1","model_version":7}""")]
    public async Task BodyThatIsNotAVersionAnswerIsAnAbsentVersion(string body)
    {
        using var service = FakeService.Answering(Reply.Ok(body), Reply.Ok("{}"));

        var version = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Absent, version);
    }

    [Fact]
    public async Task AnswerCutOffInTheMiddleIsAnAbsentVersion()
    {
        using var service = new RawService("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 500\r\n\r\n{\"service_");

        var version = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Absent, version);
    }

    [Fact]
    public async Task AnswerThatIsNotHttpIsAnAbsentVersion()
    {
        using var service = new RawService("not http\r\n\r\n");

        var version = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Absent, version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ModelNullOrBlankIsAnAbsentVersionAndNotAnEmptyOne(string? model)
    {
        using var service = FakeService.Answering(Reply.VersionOfModel(model), Reply.Ok("{}"));

        var version = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Absent, version);
    }

    [Fact]
    public async Task ServiceChangingItsAnswerBetweenTwoReadsChangesTheVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = FakeService.WithVersions(
            [Reply.VersionOfModel("pt-model-1"), Reply.VersionOfModel("pt-model-2"), Reply.VersionOfModel(null)], Reply.Ok("{}"));
        var reader = Reader(service.Port, Generous);

        var first = await reader.ReadAsync(cancellationToken);
        var second = await reader.ReadAsync(cancellationToken);
        var third = await reader.ReadAsync(cancellationToken);

        Assert.Equal([VersionValue.Present("pt-model-1"), VersionValue.Present("pt-model-2"), VersionValue.Absent], [first, second, third]);
    }

    [Fact]
    public async Task ServiceThatFailsAfterAnsweringKeepsNoVersionFromBefore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = FakeService.WithVersions([Reply.VersionOfModel("pt-model-1"), Reply.Unauthorized], Reply.Ok("{}"));
        var reader = Reader(service.Port, Generous);
        _ = await reader.ReadAsync(cancellationToken);

        var second = await reader.ReadAsync(cancellationToken);

        Assert.Equal(VersionValue.Absent, second);
    }

    [Fact]
    public async Task RequestCarriesTheSecretInTheVersionRoute()
    {
        using var service = FakeService.Answering(Reply.VersionOfModel("pt-model-3.2"), Reply.Ok("{}"));

        _ = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        var seen = Assert.Single(service.Seen);
        Assert.Equal(("GET", "/version", Reply.Secret), (seen.Method, seen.Path, seen.Secret));
    }

    [Fact(Timeout = Hang)]
    public async Task CallerCancellationIsThrownAndNotTurnedIntoAnAbsentVersion()
    {
        using var service = FakeService.Answering(
            Reply.VersionOfModel("pt-model-3.2") with { Delay = TimeSpan.FromSeconds(30) }, Reply.Ok("{}"));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var reading = Reader(service.Port, Generous).ReadAsync(caller.Token);

        await caller.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
    }

    public void Dispose() => client.Dispose();

    private HttpModelVersionReader Reader(int port, TimeSpan timeout) => new(client, new HttpRecognizerSettings(port, Reply.Secret, timeout));
}
