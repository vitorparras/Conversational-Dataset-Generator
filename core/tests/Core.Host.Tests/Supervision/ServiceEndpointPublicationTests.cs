using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>The port and the secret of the running service are one value: whoever reads them during a restart never gets the port of one process with the secret of another.</summary>
public sealed class ServiceEndpointPublicationTests : IDisposable
{
    private const int Restarts = 3;

    private readonly SupervisionHarness harness = new();

    public void Dispose() => harness.Dispose();

    [Trait("Category", "RealService")]
    [Fact]
    public async Task EndpointReadWhileTheServiceRestartsPairsEverySecretWithOnePort()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(new ServiceProcessWatch(), cancellationToken);
        using var reading = new CancellationTokenSource();
        var samples = Task.Run(() => Sample(supervisor, reading.Token), CancellationToken.None);

        for (var restart = 0; restart < Restarts; restart++)
        {
            _ = await supervisor.RestartAsync(new RestartRequest(RestartRequester.Operator, Guid.NewGuid()), SupervisionHarness.Timeout, cancellationToken);
        }

        await reading.CancelAsync();
        var seen = await samples;
        Assert.All(seen.GroupBy(endpoint => endpoint.Secret), secret => Assert.Single(secret.Select(endpoint => endpoint.Port).Distinct()));
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task PortAndSecretOfTheRunningServiceComeFromItsEndpoint()
    {
        var supervisor = await harness.StartedSupervisorAsync(new ServiceProcessWatch(), TestContext.Current.CancellationToken);

        var endpoint = supervisor.Endpoint;

        Assert.Equal((endpoint?.Port, endpoint?.Secret), (supervisor.Port, supervisor.Secret));
    }

    private static List<ServiceEndpoint> Sample(LanguageServiceSupervisor supervisor, CancellationToken stop)
    {
        var seen = new List<ServiceEndpoint>();
        while (!stop.IsCancellationRequested)
        {
            if (supervisor.Endpoint is { } endpoint)
            {
                seen.Add(endpoint);
            }
        }

        return seen;
    }
}
