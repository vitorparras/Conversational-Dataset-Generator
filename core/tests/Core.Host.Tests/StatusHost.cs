using System.Text.Json;
using Core.Host.Endpoints;
using Core.Host.Supervision;
using Core.Platform;
using Core.Platform.Configuration;
using Core.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>The host composition with a startup report, a watch and the recognizer in force given, serving only the status route in process.</summary>
internal sealed class StatusHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly HttpClient client;

    private StatusHost(WebApplication app, Uri address)
    {
        this.app = app;
        client = new HttpClient { BaseAddress = address };
    }

    public static Task<StatusHost> StartAsync(StartupReport report, ServiceProcessWatch watch, CancellationToken cancellationToken) =>
        StartAsync(report, watch, RecognizerKind.Http, cancellationToken);

    public static Task<StatusHost> StartAsync(
        StartupReport report, ServiceProcessWatch watch, RecognizerKind recognizer, CancellationToken cancellationToken) =>
        StartAsync(report, watch, new Wiring(recognizer, new Installation(InstallationKind.Real)), cancellationToken);

    public static Task<StatusHost> StartAsync(
        StartupReport report, ServiceProcessWatch watch, Installation installation, CancellationToken cancellationToken) =>
        StartAsync(report, watch, new Wiring(RecognizerKind.Http, installation), cancellationToken);

    private static async Task<StatusHost> StartAsync(
        StartupReport report, ServiceProcessWatch watch, Wiring wiring, CancellationToken cancellationToken)
    {
        var address = HostProcess.FreePlainAddress();
        var builder = CompositionRoot.ClosedBuilder(address.GetLeftPart(UriPartial.Authority));
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        _ = builder.Services.AddSingleton(report);
        _ = builder.Services.AddSingleton(watch);
        _ = builder.Services.AddSingleton(wiring.Installation);
        var app = builder.Build();
        StatusEndpoint.Map(app, wiring.Recognizer);
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

    private sealed record Wiring(RecognizerKind Recognizer, Installation Installation);
}
