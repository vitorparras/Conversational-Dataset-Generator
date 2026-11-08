using System.Text.Json;
using Core.Host.Endpoints;
using Core.Host.Supervision;
using Core.Platform;
using Core.Platform.Configuration;
using Core.Platform.Sessions;
using Core.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.Host.Tests;

/// <summary>The host composition with a startup report, a watch and the recognizer in force given, serving only the status route in process.</summary>
internal sealed class StatusHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly HttpClient client;
    private readonly RecognizerKind recognizer;

    private StatusHost(WebApplication app, Uri address, AuditComparisonState comparison, RecognizerKind recognizer)
    {
        this.app = app;
        this.recognizer = recognizer;
        Comparison = comparison;
        client = new HttpClient { BaseAddress = address };
    }

    /// <summary>The state of the last periodic comparison the status route reports; a test records one to see it said.</summary>
    public AuditComparisonState Comparison { get; }

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
        var comparison = new AuditComparisonState();
        _ = builder.Services.AddSingleton(comparison);
        _ = builder.Services.AddSingleton<SealWarningState>();
        _ = builder.Services.AddSingleton(new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System));
        var app = builder.Build();
        StatusEndpoint.Map(app, wiring.Recognizer);
        await app.StartAsync(cancellationToken);
        return new StatusHost(app, address, comparison, wiring.Recognizer);
    }

    /// <summary>
    /// The detail the route gives a session of the technical administration or the audit, from the same call the route makes (#632), written as the host writes JSON:
    /// the host of the test has no registry, so no session of it reaches the detail over the route.
    /// </summary>
    public JsonDocument Detail()
    {
        var services = app.Services;
        var sources = new StatusSources(
            services.GetRequiredService<StartupReport>(), services.GetRequiredService<ServiceProcessWatch>(), services.GetRequiredService<Installation>(), Comparison,
            services.GetRequiredService<SealWarningState>());
        var options = services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        return JsonSerializer.SerializeToDocument(SystemState.Status(sources, recognizer), options);
    }

    /// <summary>What the route answers with no session: alive and ready alone.</summary>
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
