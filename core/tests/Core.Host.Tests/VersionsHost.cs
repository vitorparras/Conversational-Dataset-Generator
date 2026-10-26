using System.Text.Json;
using Core.Boundary.Http;
using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Core.Platform.Versions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>The host composition with a build, the model version reader and, when given, the schema version, serving only the versions route in process.</summary>
internal sealed class VersionsHost : IAsyncDisposable
{
    private static readonly BuildInfo Build = new(
        VersionValue.Present("9.8.7"), VersionValue.Present("0123456789abcdef0123456789abcdef01234567"), WorkingTree.Clean);

    private readonly WebApplication app;
    private readonly HttpClient client;

    private VersionsHost(WebApplication app, Uri address)
    {
        this.app = app;
        client = new HttpClient { BaseAddress = address };
    }

    public static async Task<VersionsHost> StartAsync(
        IModelVersionReader reader, CancellationToken cancellationToken, VersionValue? schema = null)
    {
        var address = HostProcess.FreeAddress();
        var builder = CompositionRoot.ClosedBuilder(address.GetLeftPart(UriPartial.Authority));
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        _ = builder.Services.AddSingleton(new VersionReport(Build).WithSchemaVersion(schema ?? VersionValue.Absent));
        _ = builder.Services.AddSingleton(reader);
        var app = builder.Build();
        VersionsEndpoint.Map(app);
        await app.StartAsync(cancellationToken);
        return new VersionsHost(app, address);
    }

    public async Task<JsonDocument> VersionsAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(new Uri(VersionsEndpoint.Route, UriKind.Relative), cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
    }
}
