using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Core.Persistence.Custody;
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
    private readonly Task<string[]> streams;
    private readonly string logFile = Path.Combine(Path.GetTempPath(), $"zqx-sessao-{Guid.NewGuid():N}.log");
    private readonly HttpClient client;

    private SessionScenario(ScratchDatabase database, NpgsqlDataSource application, Uri address, ProcessStartInfo start)
    {
        this.database = database;
        this.application = application;
        start.ArgumentList.Add($"--Host:LogFile={logFile}");
        process = Process.Start(start) ?? throw new InvalidOperationException("The host process did not start.");
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
        var scenario = new SessionScenario(database, application, address, KeyedHost.StartInfo(address, database.AppConnectionString, idleTimeout, workFactor));
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

    /// <summary>Stops the host and hands over everything it wrote to its output streams and to its log file.</summary>
    public async Task<string> StopAndReadEverythingAsync()
    {
        process.Kill(entireProcessTree: true);
        var written = await streams.WaitAsync(DrainTimeout, CancellationToken.None);
        var file = File.Exists(logFile) ? await File.ReadAllTextAsync(logFile, CancellationToken.None) : string.Empty;
        return string.Concat(written) + file;
    }

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
        File.Delete(logFile);
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
        var active = (await registry.CreateAsync(new NewPerson(Name, function), CustodyAuthor.System, cancellationToken)).PersonId!.Value;
        _ = await credentials.SetPasswordAsync(active, Password, CustodyAuthor.System, cancellationToken);
        _ = await registry.CreateAsync(new NewPerson(NameWithoutPassword, function), CustodyAuthor.System, cancellationToken);
        var deactivated = (await registry.CreateAsync(new NewPerson(NameDeactivated, function), CustodyAuthor.System, cancellationToken)).PersonId!.Value;
        _ = await credentials.SetPasswordAsync(deactivated, Password, CustodyAuthor.System, cancellationToken);
        _ = await registry.DeactivateAsync(deactivated, CustodyAuthor.System, cancellationToken);
        _ = await KeyedHost.StatusAsync(address, process, cancellationToken);
    }
}
