using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>
/// The host composition serving, in process and in clear, the routes a test maps behind the real session gate. The store is the test's own and a session is
/// seeded straight into it, so no database is reached and no password derived. A request that carries <see cref="EncryptedMarker"/> reaches the gate as one
/// that came over the encrypted channel, which the process cannot serve here; one without it comes in clear.
/// </summary>
internal sealed class SessionedHost : IAsyncDisposable
{
    public const string EncryptedMarker = "X-Zqx-Encrypted";
    private const string Cookie = "__Host-sessao";

    private readonly WebApplication app;
    private readonly HttpClient client;

    private SessionedHost(WebApplication app, SessionStore sessions, Uri address)
    {
        this.app = app;
        Sessions = sessions;
        client = new HttpClient { BaseAddress = address };
    }

    /// <summary>The store the gate asks, which the test seeds.</summary>
    public SessionStore Sessions { get; }

    /// <summary>The host with the fake recognizer and the analysis route, behind the gate.</summary>
    public static Task<SessionedHost> StartWithAnalysisAsync(CancellationToken cancellationToken) =>
        StartAsync(services => services.AddSingleton<IRecognizer, FakeRecognizer>().AddTransient<Host.Analysis.MaterialAnalysis>(), AnalysisEndpoint.Map, cancellationToken);

    public static async Task<SessionedHost> StartAsync(
        Func<IServiceCollection, IServiceCollection> services, Action<IEndpointRouteBuilder> routes, CancellationToken cancellationToken)
    {
        var address = HostProcess.FreePlainAddress();
        var builder = CompositionRoot.ClosedBuilder(address.GetLeftPart(UriPartial.Authority));
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        var sessions = new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System);
        _ = builder.Services.AddSingleton(sessions);
        _ = services(builder.Services);
        var app = builder.Build();
        _ = app.Use(async (context, next) =>
        {
            context.Request.Scheme = context.Request.Headers.ContainsKey(EncryptedMarker) ? "https" : context.Request.Scheme;
            await next(context);
        });
        SessionGate.Use(app);
        routes(app);
        await app.StartAsync(cancellationToken);
        return new SessionedHost(app, sessions, address);
    }

    /// <summary>A session of a person who entered, as the store keeps it, and the identifier that names it.</summary>
    public string SeedSession() => Sessions.Open(personId: 1);

    public async Task<HttpResponseMessage> GetAsync(string route, string? identifier, bool encrypted, CancellationToken cancellationToken)
    {
        using var request = RequestOf(HttpMethod.Get, route, identifier, encrypted);
        return await client.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsync(string route, string json, string? identifier, CancellationToken cancellationToken)
    {
        using var request = RequestOf(HttpMethod.Post, route, identifier, encrypted: true);
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await client.SendAsync(request, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
    }

    private static HttpRequestMessage RequestOf(HttpMethod method, string route, string? identifier, bool encrypted)
    {
        var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));
        if (identifier is not null)
        {
            request.Headers.Add("Cookie", $"{Cookie}={identifier}");
        }

        if (encrypted)
        {
            request.Headers.Add(EncryptedMarker, "1");
        }

        return request;
    }
}
