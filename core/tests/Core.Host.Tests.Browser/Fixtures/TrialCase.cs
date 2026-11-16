using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Host.Tests.Browser.Trial;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the trial: a host composition on a real Kestrel, on a loopback port the system gives, over a migrated database of its own, with the routes of the screens
/// and the ones that exist for the test alone. A browser reaches it by <see cref="Address"/>; the database and the server go with the object.
/// </summary>
internal sealed class TrialCase : IAsyncDisposable
{
    private readonly ScreenHost host;
    private readonly HostLog log;

    private TrialCase(ScreenHost host, HostLog log)
    {
        this.host = host;
        this.log = log;
    }

    /// <summary>The address the server listens on, with the port it was given.</summary>
    public Uri Address => host.Address;

    /// <summary>The lines the host logged, with the id of their event.</summary>
    public IReadOnlyList<string> Log => log.Lines;

    public static async Task<TrialCase> StartAsync(CancellationToken cancellationToken)
    {
        var log = new HostLog();
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                WithPeople = false,
                Services = (services, _) => services.AddLogging(logging => logging.AddProvider(log)),
                Pipeline = (started, _) =>
                {
                    ScreenFailure.Use(started);
                    ScreenAssets.Map(started);
                    TrialRoutes.Map(started);
                },
            },
            cancellationToken);
        return new TrialCase(host, log);
    }

    public ValueTask DisposeAsync() => host.DisposeAsync();
}
