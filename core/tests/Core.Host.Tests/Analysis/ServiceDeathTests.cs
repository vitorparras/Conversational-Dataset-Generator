using Core.Boundary.Contracts;
using Core.Boundary.Http;
using Core.Host.Analysis;
using Core.Host.Supervision;
using Core.Host.Tests.Supervision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests.Analysis;

/// <summary>
/// A language service that dies in the middle of an analysis leaves it refused with the cause and nothing to keep; once
/// the service is started again, the analysis the composition root registers asks the new process at its new port with
/// its new secret (issue #32, CA-2 and CA-5).
/// </summary>
public sealed class ServiceDeathTests : IDisposable
{
    private static readonly AnalysisAsked Asked = new("falei com PESSOA_1 ontem ZQX-MARKER-0027-DEATH", "pt");

    private readonly SupervisionHarness harness = new();
    private readonly ServiceProcessWatch watch = new();

    [Fact]
    public async Task ServiceThatDiesWithTheRequestInFlightLeavesTheAnalysisRefusedWithTheCause()
    {
        using var service = new DyingService();
        var settings = new HttpRecognizerSettings(service.Port, service.Secret, SupervisionHarness.Timeout);
        var recognizer = new HttpRecognizer(harness.Client, settings, NullLogger<HttpRecognizer>.Instance);

        var result = await new MaterialAnalysis(recognizer, new AnalysisLog()).AnalyzeAsync(Asked, TestContext.Current.CancellationToken);

        Assert.Null(result.Concluded);
        Assert.Equal(UnavailabilityReason.MalformedResponse, result.Refused!.Reason);
        Assert.Contains("encerra com o pedido em curso", result.Refused.Cause, StringComparison.Ordinal);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task KilledServiceLeavesTheNextAnalysisRefusedAsOutOfReach()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        using var container = HttpContainer(supervisor);

        StandInService.Kill(supervisor.ProcessId!.Value);
        var lost = await Poll.UntilAsync(() => Task.FromResult(watch.State == ServiceState.Unavailable), Poll.LossLimit, cancellationToken);
        Assert.True(lost, "the end of the service process was not seen");
        var result = await container.GetRequiredService<MaterialAnalysis>().AnalyzeAsync(Asked, cancellationToken);

        Assert.Null(result.Concluded);
        Assert.Equal(UnavailabilityReason.ConnectionRefused, result.Refused!.Reason);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task ServiceThatDiedWhileAnotherProcessHoldsItsPortLeavesTheAnalysisRefusedAndSendsNothingThere()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        var port = supervisor.Port!.Value;
        using var container = HttpContainer(supervisor);
        StandInService.Kill(supervisor.ProcessId!.Value);
        var lost = await Poll.UntilAsync(() => Task.FromResult(watch.State == ServiceState.Unavailable), Poll.LossLimit, cancellationToken);
        Assert.True(lost, "the end of the service process was not seen");
        using var stranger = new StrangerOnThePort(port);

        var result = await container.GetRequiredService<MaterialAnalysis>().AnalyzeAsync(Asked, cancellationToken);

        Assert.Null(result.Concluded);
        Assert.Equal(UnavailabilityReason.ConnectionRefused, result.Refused!.Reason);
        Assert.Contains("não pôde ser alcançado", result.Refused.Cause, StringComparison.Ordinal);
        Assert.Equal(0, stranger.Connections);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task ServiceThatDiedIsNoLongerOfferedItsPortOrItsSecret()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);

        StandInService.Kill(supervisor.ProcessId!.Value);
        var lost = await Poll.UntilAsync(() => Task.FromResult(watch.State == ServiceState.Unavailable), Poll.LossLimit, cancellationToken);

        Assert.True(lost, "the end of the service process was not seen");
        Assert.Null(supervisor.Endpoint);
        Assert.Null(supervisor.Port);
        Assert.Empty(supervisor.Secret);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task AnalysisAfterARestartAsksTheNewProcessAtItsPortWithItsSecret()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        using var container = HttpContainer(supervisor);
        var before = await container.GetRequiredService<MaterialAnalysis>().AnalyzeAsync(Asked, cancellationToken);
        var restart = new RestartRequest(RestartRequester.Operator, Guid.NewGuid());
        _ = await supervisor.RestartAsync(restart, SupervisionHarness.Timeout, cancellationToken);

        var after = await container.GetRequiredService<MaterialAnalysis>().AnalyzeAsync(Asked, cancellationToken);

        Assert.NotNull(before.Concluded);
        Assert.Null(after.Refused);
        Assert.Equal(0, after.Concluded!.Occurrences);
    }

    public void Dispose() => harness.Dispose();

    private ServiceProvider HttpContainer(LanguageServiceSupervisor supervisor) =>
        RecognizerContainer.Of(RecognizerContainer.Loaded("--Recognizer:Kind=http").Values, supervisor, harness.Client);
}
