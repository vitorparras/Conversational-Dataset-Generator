using System.Text.Json;
using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Core.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>The host composition with a startup report given, serving only the status route in process.</summary>
internal sealed class StatusHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly HttpClient client;

    private StatusHost(WebApplication app, Uri address)
    {
        this.app = app;
        client = new HttpClient { BaseAddress = address };
    }

    public static async Task<StatusHost> StartAsync(StartupReport report, CancellationToken cancellationToken)
    {
        var address = HostProcess.FreeAddress();
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseUrls(address.GetLeftPart(UriPartial.Authority));
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View);
        _ = builder.Services.AddSingleton(report);
        var app = builder.Build();
        StatusEndpoint.Map(app);
        await app.StartAsync(cancellationToken);
        return new StatusHost(app, address);
    }

    public async Task<JsonDocument> StatusAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(new Uri(StatusEndpoint.Route, UriKind.Relative), cancellationToken);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
    }
}
