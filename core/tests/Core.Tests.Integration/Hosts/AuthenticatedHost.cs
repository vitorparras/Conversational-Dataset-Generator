using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Core.Host.Tests;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Tests.Integration.Privacy;
using Core.Tests.Support.Custody;
using Core.Tests.Support.Net;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The built host as its own process over a database of its own, migrated, holding one person with a password; the test enters once, when the host is up,
/// and every request it sends afterwards carries the session. The host, the database and the copy of the binary go with the object.
/// </summary>
internal sealed class AuthenticatedHost : IAsyncDisposable
{
    private const string Cookie = "__Host-sessao";
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Where a mutation run collects which tests reach which code. The host built beside the tests is instrumented under
    /// that run and would otherwise write to the test process's files and hold back its coverage until it times out.
    /// </summary>
    private static readonly string[] CoverageVariables = ["STRYKER_COVERAGE_FILE", "STRYKER_COVERAGE_EPOCH_FILE"];

    private readonly ScratchDatabase database;
    private readonly NpgsqlDataSource application;
    private readonly Process process;
    private readonly Task<string[]> streams;
    private readonly HttpClient client;
    private readonly DirectoryInfo? copy;
    private string identifier = string.Empty;

    private AuthenticatedHost(ScratchDatabase database, NpgsqlDataSource application, HostSetup setup, Uri address)
    {
        this.database = database;
        this.application = application;
        Address = address;
        if (setup.SettingsJson is not null)
        {
            copy = Directory.CreateTempSubdirectory("zqx-host-copy-");
            foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory))
            {
                File.Copy(file, Path.Combine(copy.FullName, Path.GetFileName(file)));
            }

            File.WriteAllText(Path.Combine(copy.FullName, "appsettings.json"), setup.SettingsJson);
        }

        LogFile = setup.WithLogFile ? Path.Combine(Path.GetTempPath(), $"zqx-host-{Guid.NewGuid():N}.log") : null;
        process = Process.Start(StartInfo(setup, address, database.AppConnectionString, copy?.FullName, LogFile))
            ?? throw new InvalidOperationException("The host process did not start.");
        streams = Task.WhenAll(process.StandardError.ReadToEndAsync(CancellationToken.None), process.StandardOutput.ReadToEndAsync(CancellationToken.None));
        client = new HttpClient(SharedTlsMaterial.PinnedHandler()) { BaseAddress = address };
    }

    public Uri Address { get; }

    /// <summary>The file the host was asked to write its log to; null when it was not asked.</summary>
    public string? LogFile { get; }

    public string Name { get; } = $"zqx-pessoa-{Guid.NewGuid():N}";

    public string Password { get; } = $"zqx-{Guid.NewGuid():N}";

    /// <summary>What the log file holds so far; empty when the host was not asked to write one.</summary>
    public string LogText => LogFile is not null && File.Exists(LogFile) ? ReadShared(LogFile) : string.Empty;

    public static async Task<AuthenticatedHost> StartAsync(HostSetup setup, CancellationToken cancellationToken)
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        var application = NpgsqlDataSource.Create(database.AppConnectionString);
        var host = new AuthenticatedHost(database, application, setup, new Uri($"https://127.0.0.1:{KeyedHost.FreePort()}"));
        try
        {
            await host.PrepareAsync(setup.WorkFactor, cancellationToken);
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public Task<HttpResponseMessage> GetAsync(string route, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, route, json: null, cancellationToken);

    public Task<HttpResponseMessage> PostAsync(string route, string json, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, route, json, cancellationToken);

    /// <summary>Kills the host and its tree and hands over what it wrote to its output and error streams.</summary>
    public async Task<(string Stdout, string Stderr)> StopAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        var written = await streams.WaitAsync(DrainTimeout, CancellationToken.None);
        return (Stdout: written[1], Stderr: written[0]);
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
        if (LogFile is not null)
        {
            File.Delete(LogFile);
        }

        copy?.Delete(recursive: true);
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static ProcessStartInfo StartInfo(HostSetup setup, Uri address, string databaseConnectionString, string? binaryDirectory, string? logFile)
    {
        var database = new NpgsqlConnectionStringBuilder(databaseConnectionString);
        var directory = binaryDirectory ?? AppContext.BaseDirectory;
        var info = new ProcessStartInfo(Path.Combine(directory, OperatingSystem.IsWindows() ? "Core.Host.exe" : "Core.Host"))
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var name in CoverageVariables)
        {
            _ = info.Environment.Remove(name);
        }

        info.Environment["DOTNET_ENVIRONMENT"] = "Development";
        info.Environment["CDG_Database__Host"] = $"{database.Host}:{database.Port}";
        info.Environment["CDG_Database__Name"] = database.Database;
        info.Environment["CDG_Database__Username"] = database.Username;
        info.Environment["CDG_Database__Password"] = database.Password;
        foreach (var variable in setup.Environment)
        {
            info.Environment[variable.Key] = variable.Value;
        }

        foreach (var argument in Arguments(setup, address, logFile))
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    /// <summary>The defaults every test needs, less the keys the setup omits or gives itself, then the arguments the setup adds.</summary>
    private static List<string> Arguments(HostSetup setup, Uri address, string? logFile)
    {
        var defaults = new Dictionary<string, string>
        {
            ["Host:Urls"] = address.GetLeftPart(UriPartial.Authority),
            ["Installation:Kind"] = "demonstracao",
            ["Passwords:WorkFactor"] = setup.WorkFactor.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Session:IdleTimeout"] = "00:30:00",
            ["Session:AbsoluteTimeout"] = "08:00:00",
            ["Tls:CertificateFile"] = SharedTlsMaterial.CertificatePath,
            ["Tls:KeyFile"] = SharedTlsMaterial.KeyPath,
            ["MasterKey:File"] = CustodyFiles.Shared.KeyPath,
            ["MasterKey:LabelsFile"] = CustodyFiles.Shared.LabelsPath,
        };
        if (setup.RealService)
        {
            defaults["LanguageService:Executable"] = LanguageServiceInterpreter.Path;
        }
        else
        {
            defaults["Recognizer:Kind"] = "fake";
            defaults["LanguageService:Executable"] = "zqx-never-started-7731";
        }

        if (logFile is not null)
        {
            defaults["Host:LogFile"] = logFile;
        }

        var given = setup.Arguments.Select(KeyOf).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. defaults.Where(entry => !given.Contains(entry.Key) && !setup.Omit.Contains(entry.Key)).Select(entry => $"--{entry.Key}={entry.Value}"),
            .. setup.Arguments,
        ];
    }

    private static string KeyOf(string argument) => argument.TrimStart('-')[..argument.TrimStart('-').IndexOf('=', StringComparison.Ordinal)];

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, string? json, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));
        request.Headers.Add("Cookie", $"{Cookie}={identifier}");
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private async Task PrepareAsync(int workFactor, CancellationToken cancellationToken)
    {
        var registry = new PersonRegistry(application);
        var credentials = new PersonCredentials(application, new PasswordDerivation(workFactor));
        var person = (await registry.CreateAsync(new NewPerson(Name, PersonFunctions.Accepted[0]), CustodyAuthor.System, cancellationToken)).PersonId!.Value;
        _ = await credentials.SetPasswordAsync(person, Password, CustodyAuthor.System, cancellationToken);
        _ = await KeyedHost.StatusAsync(Address, process, cancellationToken);
        using var entry = new HttpRequestMessage(HttpMethod.Post, new Uri("/session", UriKind.Relative))
        {
            Content = new StringContent($$"""{"name":"{{Name}}","password":"{{Password}}"}""", Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        using var response = await client.SendAsync(entry, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var line = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        identifier = line[(Cookie.Length + 1)..line.IndexOf(';', StringComparison.Ordinal)];
    }
}
