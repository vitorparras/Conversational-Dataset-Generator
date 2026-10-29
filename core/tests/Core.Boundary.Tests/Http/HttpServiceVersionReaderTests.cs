using Core.Boundary.Http;
using Core.Platform.Versions;
using Core.Tests.Support.Net;

namespace Core.Boundary.Tests.Http;

/// <summary>
/// The versions are whatever the service says they are at the moment of the read, and nothing otherwise: any failure
/// or an answer outside the shape of a version leaves all three absent, never empty and never remembered from before,
/// and a field the service leaves blank is absent without touching the other two. The service is a stand-in on a free loopback port.
/// </summary>
public sealed class HttpServiceVersionReaderTests : IDisposable
{
    private const int Hang = 60_000;

    private static readonly TimeSpan Generous = TimeSpan.FromMinutes(5);

    private readonly HttpClient client = LoopbackHttpClient.Create();

    [Fact]
    public async Task VersionsTheServiceDeclaresArePresentEachAsDeclared()
    {
        using var service = FakeService.Answering(Reply.Versions("0.4.2", "7", "pt-model-3.2"), Reply.Ok("{}"));

        var versions = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Of("7", "0.4.2", "pt-model-3.2"), versions);
    }

    [Fact]
    public async Task ContractTheServiceDeclaresIsNotTheOneTheCoreSpeaks()
    {
        using var service = FakeService.Answering(Reply.Versions("0.4.2", "99", "pt-model-3.2"), Reply.Ok("{}"));

        var versions = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Present("99"), versions.Contract);
    }

    [Fact]
    public async Task PortWithNoListenerHasNoVersions()
    {
        using var closed = new ClosedPort();

        var versions = await Reader(closed.Number, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServiceVersions.Absent, versions);
    }

    [Fact]
    public async Task SecretTheServiceRefusesHasNoVersions()
    {
        using var service = FakeService.Answering(Reply.Unauthorized, Reply.Ok("{}"));

        var versions = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServiceVersions.Absent, versions);
    }

    [Fact]
    public async Task RedirectIsNotFollowedAndHasNoVersions()
    {
        using var service = FakeService.Answering(
            Reply.VersionOfModel("pt-model-3.2") with { Status = 302, Location = "http://127.0.0.1:1/version" }, Reply.Ok("{}"));

        var versions = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServiceVersions.Absent, versions);
    }

    [Fact(Timeout = Hang)]
    public async Task ServiceThatAnswersAfterTheLimitHasNoVersions()
    {
        using var service = FakeService.Answering(
            Reply.VersionOfModel("pt-model-3.2") with { Delay = TimeSpan.FromSeconds(5) }, Reply.Ok("{}"));

        var versions = await Reader(service.Port, TimeSpan.FromMilliseconds(200)).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServiceVersions.Absent, versions);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData(/*lang=json,strict*/ """{"contract_version":"1","model_version":"pt-model-3.2"}""")]
    [InlineData(/*lang=json,strict*/ """{"service_version":"0.1.0","model_version":"pt-model-3.2"}""")]
    [InlineData(/*lang=json,strict*/ """{"service_version":"0.1.0","contract_version":"1","model_version":"pt-model-3.2","extra":1}""")]
    [InlineData(/*lang=json,strict*/ """{"service_version":"0.1.0","contract_version":"1","model_version":7}""")]
    [InlineData(/*lang=json,strict*/ """{"service_version":7,"contract_version":"1","model_version":"pt-model-3.2"}""")]
    public async Task BodyThatIsNotAVersionAnswerLeavesAllThreeAbsent(string body)
    {
        using var service = FakeService.Answering(Reply.Ok(body), Reply.Ok("{}"));

        var versions = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServiceVersions.Absent, versions);
    }

    [Fact]
    public async Task AnswerCutOffInTheMiddleHasNoVersions()
    {
        using var service = new RawService("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 500\r\n\r\n{\"service_");

        var versions = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServiceVersions.Absent, versions);
    }

    [Fact]
    public async Task AnswerThatIsNotHttpHasNoVersions()
    {
        using var service = new RawService("not http\r\n\r\n");

        var versions = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServiceVersions.Absent, versions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ModelNullOrBlankIsAbsentAndTheOtherTwoStayPresent(string? model)
    {
        using var service = FakeService.Answering(Reply.Versions("0.4.2", "7", model), Reply.Ok("{}"));

        var versions = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Of("7", "0.4.2", null), versions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ContractBlankIsAbsentAndTheOtherTwoStayPresent(string contract)
    {
        using var service = FakeService.Answering(Reply.Versions("0.4.2", contract, "pt-model-3.2"), Reply.Ok("{}"));

        var versions = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Of(null, "0.4.2", "pt-model-3.2"), versions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ServiceBlankIsAbsentAndTheOtherTwoStayPresent(string serviceVersion)
    {
        using var service = FakeService.Answering(Reply.Versions(serviceVersion, "7", "pt-model-3.2"), Reply.Ok("{}"));

        var versions = await Reader(service.Port, Generous).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Of("7", null, "pt-model-3.2"), versions);
    }

    [Fact]
    public async Task ServiceChangingItsAnswerBetweenReadsChangesTheVersions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = FakeService.WithVersions(
            [Reply.Versions("0.4.1", "6", "pt-model-1"), Reply.Versions("0.4.2", "7", "pt-model-2"), Reply.Versions("0.4.2", "7", null)],
            Reply.Ok("{}"));
        var reader = Reader(service.Port, Generous);

        var first = await reader.ReadAsync(cancellationToken);
        var second = await reader.ReadAsync(cancellationToken);
        var third = await reader.ReadAsync(cancellationToken);

        Assert.Equal([Of("6", "0.4.1", "pt-model-1"), Of("7", "0.4.2", "pt-model-2"), Of("7", "0.4.2", null)], [first, second, third]);
    }

    [Fact]
    public async Task ServiceThatFailsAfterAnsweringKeepsNoVersionFromBefore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = FakeService.WithVersions([Reply.Versions("0.4.2", "7", "pt-model-1"), Reply.Unauthorized], Reply.Ok("{}"));
        var reader = Reader(service.Port, Generous);
        _ = await reader.ReadAsync(cancellationToken);

        var second = await reader.ReadAsync(cancellationToken);

        Assert.Equal(ServiceVersions.Absent, second);
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
    public async Task CallerCancellationIsThrownAndNotTurnedIntoAbsentVersions()
    {
        using var service = FakeService.Answering(
            Reply.VersionOfModel("pt-model-3.2") with { Delay = TimeSpan.FromSeconds(30) }, Reply.Ok("{}"));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var reading = Reader(service.Port, Generous).ReadAsync(caller.Token);

        await caller.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
    }

    public void Dispose() => client.Dispose();

    private static ServiceVersions Of(string? contract, string? service, string? model) =>
        new(Declared(contract), Declared(service), Declared(model));

    private static VersionValue Declared(string? version) => version is null ? VersionValue.Absent : VersionValue.Present(version);

    private HttpServiceVersionReader Reader(int port, TimeSpan timeout) => new(client, new HttpRecognizerSettings(port, Reply.Secret, timeout));
}
