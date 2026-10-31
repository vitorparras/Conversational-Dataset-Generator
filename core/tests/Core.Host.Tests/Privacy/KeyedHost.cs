using System.Collections.ObjectModel;
using System.Text.Json;
using Npgsql;

namespace Core.Host.Tests.Privacy;

/// <summary>The built host started with the synthetic key of the shared custody files, asked for its status, and stopped.</summary>
internal static class KeyedHost
{
    /// <summary>Starts the host with the key and no database, and returns what it answered at <c>/status</c>.</summary>
    public static Task<JsonDocument> RunAsync(CancellationToken cancellationToken) =>
        RunAsync(environment: ReadOnlyDictionary<string, string>.Empty, cancellationToken);

    /// <summary>Starts the host with the key and the database the connection string names, and returns what it answered at <c>/status</c>.</summary>
    public static Task<JsonDocument> RunAsync(string databaseConnectionString, CancellationToken cancellationToken)
    {
        var database = new NpgsqlConnectionStringBuilder(databaseConnectionString);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CDG_Database__Host"] = $"{database.Host}:{database.Port}",
            ["CDG_Database__Name"] = database.Database!,
            ["CDG_Database__Username"] = database.Username!,
            ["CDG_Database__Password"] = database.Password!,
        };
        return RunAsync(new ReadOnlyDictionary<string, string>(environment), cancellationToken);
    }

    /// <summary>The status of the check with that name, as the host reported it.</summary>
    public static string StatusOf(JsonDocument status, string check) =>
        status.RootElement.GetProperty("checks").EnumerateArray()
            .Single(entry => entry.GetProperty("name").GetString() == check)
            .GetProperty("status").GetString()!;

    private static async Task<JsonDocument> RunAsync(IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken)
    {
        var baseAddress = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(baseAddress) with { Environment = environment };
        using var host = await HostProcess.StartAsync(installation, baseAddress, HostProcess.StartupTimeout, cancellationToken);
        using var response = await host.GetAsync("/status", cancellationToken);
        var status = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        _ = await host.StopWithErrorAsync();
        return status;
    }
}
