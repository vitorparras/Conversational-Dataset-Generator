using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Core.Tests.Support.Audit;
using Core.Tests.Support.Custody;
using Core.Tests.Support.Net;
using Npgsql;

namespace Core.Tests.Integration.Privacy;

/// <summary>The built host started over a database with the synthetic key of the shared custody files, asked for its status, and stopped.</summary>
internal static class KeyedHost
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Where a mutation run collects which tests reach which code. The host built beside the tests is instrumented under
    /// that run and would otherwise write to the test process's files and hold back its coverage until it times out.
    /// </summary>
    private static readonly string[] CoverageVariables = ["STRYKER_COVERAGE_FILE", "STRYKER_COVERAGE_EPOCH_FILE"];

    /// <summary>Starts the host with the key and the database the connection string names, and returns what it answered at <c>/status</c>.</summary>
    public static async Task<JsonDocument> RunAsync(string databaseConnectionString, CancellationToken cancellationToken)
    {
        var address = new Uri($"https://127.0.0.1:{FreePort()}");
        using var process = Process.Start(StartInfo(address, databaseConnectionString))
            ?? throw new InvalidOperationException("The host process did not start.");
        var streams = Task.WhenAll(process.StandardError.ReadToEndAsync(CancellationToken.None), process.StandardOutput.ReadToEndAsync(CancellationToken.None));
        try
        {
            return await StatusAsync(address, process, cancellationToken);
        }
        finally
        {
            process.Kill(entireProcessTree: true);
            _ = await Task.WhenAny(streams, Task.Delay(DrainTimeout, CancellationToken.None));
        }
    }

    /// <summary>The status of the check with that name, as the host reported it.</summary>
    public static string StatusOf(JsonDocument status, string check) =>
        status.RootElement.GetProperty("checks").EnumerateArray()
            .Single(entry => entry.GetProperty("name").GetString() == check)
            .GetProperty("status").GetString()!;

    internal static async Task<JsonDocument> StatusAsync(Uri address, Process process, CancellationToken cancellationToken)
    {
        using var client = new HttpClient(SharedTlsMaterial.PinnedHandler()) { BaseAddress = address };
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < StartupTimeout)
        {
            Assert.False(process.HasExited, "The host exited before it answered.");
            if (await TryStatusAsync(client, cancellationToken) is { } status)
            {
                return status;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new TimeoutException($"The host did not answer within {StartupTimeout}.");
    }

    private static async Task<JsonDocument?> TryStatusAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(new Uri("/status", UriKind.Relative), cancellationToken);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    internal static ProcessStartInfo StartInfo(Uri address, string databaseConnectionString, string idleTimeout = "00:30:00", int workFactor = 1)
    {
        var database = new NpgsqlConnectionStringBuilder(databaseConnectionString);
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Core.Host.exe" : "Core.Host"))
        {
            WorkingDirectory = AppContext.BaseDirectory,
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
        foreach (var argument in Arguments(address, idleTimeout, workFactor))
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    private static string[] Arguments(Uri address, string idleTimeout, int workFactor) =>
    [
        $"--Host:Urls={address.GetLeftPart(UriPartial.Authority)}",
        "--Installation:Kind=demonstracao",
        $"--Passwords:WorkFactor={workFactor}",
        $"--Session:IdleTimeout={idleTimeout}",
        "--Session:AbsoluteTimeout=08:00:00",
        "--Recognizer:Kind=fake",
        "--LanguageService:Executable=zqx-never-started-7731",
        $"--Tls:CertificateFile={SharedTlsMaterial.CertificatePath}",
        $"--Tls:KeyFile={SharedTlsMaterial.KeyPath}",
        $"--Audit:ServerLogDirectory={ServerLogFiles.Shared}",
        $"--MasterKey:File={CustodyFiles.Shared.KeyPath}",
        $"--MasterKey:LabelsFile={CustodyFiles.Shared.LabelsPath}",
    ];

    internal static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
