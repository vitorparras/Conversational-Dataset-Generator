using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Tests.Integration.Privacy;
using Core.Tests.Support.Net;
using Npgsql;

namespace Core.Tests.Integration.Sessions;

/// <summary>
/// The built host over a database of its own, migrated, holding an active person with a password, one without a password and one deactivated, each with a name no
/// other test uses; the password is made when the test runs, so that no credential is written in the repository. The host and the database go with the scenario.
/// </summary>
internal sealed class SessionScenario : IAsyncDisposable
{
    public const string Cookie = "__Host-sessao";

    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private readonly ScratchDatabase database;
    private readonly NpgsqlDataSource application;
    private readonly Process process;
    private readonly Task streams;
    private readonly HttpClient client;

    private SessionScenario(ScratchDatabase database, NpgsqlDataSource application, Process process, Uri address)
    {
        this.database = database;
        this.application = application;
        this.process = process;
        streams = Task.WhenAll(process.StandardError.ReadToEndAsync(CancellationToken.None), process.StandardOutput.ReadToEndAsync(CancellationToken.None));
        var handler = SharedTlsMaterial.PinnedHandler();
        handler.UseCookies = false;
        client = new HttpClient(handler) { BaseAddress = address };
    }

    public string Name { get; } = $"zqx-pessoa-{Guid.NewGuid():N}";

    public string Password { get; } = $"zqx-{Guid.NewGuid():N}";

    public string NameWithoutPassword { get; } = $"zqx-pessoa-{Guid.NewGuid():N}";

    public string NameDeactivated { get; } = $"zqx-pessoa-{Guid.NewGuid():N}";

    public static Task<SessionScenario> StartAsync(string idleTimeout, CancellationToken cancellationToken) => StartAsync(idleTimeout, workFactor: 1, cancellationToken);

    public static async Task<SessionScenario> StartAsync(string idleTimeout, int workFactor, CancellationToken cancellationToken)
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        var application = NpgsqlDataSource.Create(database.AppConnectionString);
        var address = new Uri($"https://127.0.0.1:{KeyedHost.FreePort()}");
        var process = Process.Start(KeyedHost.StartInfo(address, database.AppConnectionString, idleTimeout, workFactor))
            ?? throw new InvalidOperationException("The host process did not start.");
        var scenario = new SessionScenario(database, application, process, address);
        try
        {
            await scenario.PrepareAsync(address, workFactor, cancellationToken);
            return scenario;
        }
        catch
        {
            await scenario.DisposeAsync();
            throw;
        }
    }

    /// <summary>The entry with the name and the password, as the JSON the route takes.</summary>
    public Task<HttpResponseMessage> EnterAsync(string name, string password, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, null, $$"""{"name":"{{name}}","password":"{{password}}"}""", cancellationToken);

    public Task<HttpResponseMessage> ProbeAsync(string? identifier, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, identifier, json: null, cancellationToken);

    public Task<HttpResponseMessage> LeaveAsync(string? identifier, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, identifier, json: null, cancellationToken);

    /// <summary>The whole Set-Cookie line the entry answered with.</summary>
    public static string CookieLineOf(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"));

    /// <summary>The identifier the entry handed to the browser.</summary>
    public static string IdentifierOf(HttpResponseMessage response)
    {
        var line = CookieLineOf(response);
        var start = Cookie.Length + 1;
        return line[start..line.IndexOf(';', StringComparison.Ordinal)];
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        _ = await Task.WhenAny(streams, Task.Delay(DrainTimeout, CancellationToken.None));
        process.Dispose();
        await application.DisposeAsync();
        await database.DisposeAsync();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string? identifier, string? json, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri("/session", UriKind.Relative));
        if (identifier is not null)
        {
            request.Headers.Add("Cookie", $"{Cookie}={identifier}");
        }

        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private async Task PrepareAsync(Uri address, int workFactor, CancellationToken cancellationToken)
    {
        var registry = new PersonRegistry(application);
        var credentials = new PersonCredentials(application, new PasswordDerivation(workFactor));
        var function = PersonFunctions.Accepted[0];
        var active = (await registry.CreateAsync(new NewPerson(Name, function), actorId: null, cancellationToken)).PersonId!.Value;
        _ = await credentials.SetPasswordAsync(active, Password, actorId: null, cancellationToken);
        _ = await registry.CreateAsync(new NewPerson(NameWithoutPassword, function), actorId: null, cancellationToken);
        var deactivated = (await registry.CreateAsync(new NewPerson(NameDeactivated, function), actorId: null, cancellationToken)).PersonId!.Value;
        _ = await credentials.SetPasswordAsync(deactivated, Password, actorId: null, cancellationToken);
        _ = await registry.DeactivateAsync(deactivated, actorId: null, cancellationToken);
        _ = await KeyedHost.StatusAsync(address, process, cancellationToken);
    }
}
