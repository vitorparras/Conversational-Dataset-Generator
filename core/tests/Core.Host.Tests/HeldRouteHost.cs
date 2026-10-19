using System.Net.Sockets;
using Core.Platform.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Core.Host.Tests;

/// <summary>The host composition serving, in process, one route that holds each request until the test lets it go.</summary>
internal sealed class HeldRouteHost : IAsyncDisposable
{
    public const string Answer = "ZQX-DONE-7731";
    private const string Route = "/zqx-held-7731";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    private readonly WebApplication app;
    private readonly Uri address;
    private readonly HttpClient client;
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task running = Task.CompletedTask;

    private HeldRouteHost(WebApplication app, Uri address)
    {
        this.app = app;
        this.address = address;
        client = new HttpClient { BaseAddress = address };
    }

    public static async Task<HeldRouteHost> StartAsync(CancellationToken cancellationToken)
    {
        var address = HostProcess.FreeAddress();
        var builder = CompositionRoot.ClosedBuilder(address.GetLeftPart(UriPartial.Authority));
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        var host = new HeldRouteHost(builder.Build(), address);
        _ = host.app.MapGet(Route, host.HoldAsync);
        await host.app.StartAsync(cancellationToken);
        host.running = host.app.WaitForShutdownAsync(CancellationToken.None);
        return host;
    }

    /// <summary>Sends a request to the held route and returns once the route holds it, with the answer still to come.</summary>
    public async Task<Task<string>> HeldRequestAsync(CancellationToken cancellationToken)
    {
        var answer = client.GetStringAsync(new Uri(Route, UriKind.Relative), cancellationToken);
        await entered.Task.WaitAsync(cancellationToken);
        return answer;
    }

    /// <summary>Asks the host to stop as a termination signal does, and returns once it no longer takes new connections.</summary>
    public async Task AskToStopAsync(CancellationToken cancellationToken)
    {
        app.Lifetime.StopApplication();
        while (await AcceptsAsync(cancellationToken))
        {
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    /// <summary>Lets the held request go and returns once the host has stopped.</summary>
    public async Task ReleaseAndWaitForStopAsync()
    {
        _ = released.TrySetResult();
        await running;
    }

    public async ValueTask DisposeAsync()
    {
        _ = released.TrySetResult();
        app.Lifetime.StopApplication();
        await running;
        await app.DisposeAsync();
        client.Dispose();
    }

    private async Task<string> HoldAsync(CancellationToken requestAborted)
    {
        _ = entered.TrySetResult();
        await released.Task.WaitAsync(requestAborted);
        return Answer;
    }

    private async Task<bool> AcceptsAsync(CancellationToken cancellationToken)
    {
        using var probe = new TcpClient();
        try
        {
            await probe.ConnectAsync(address.Host, address.Port, cancellationToken);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
