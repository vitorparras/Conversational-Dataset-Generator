using System.Collections.ObjectModel;

namespace Core.Host.Tests.Privacy;

/// <summary>The built host started with the synthetic key of the shared custody files, asked for its status until it answered, and stopped, with what it wrote.</summary>
internal static class KeyedHost
{
    /// <summary>Starts the host with the key, waits for it to answer at <c>/status</c>, and returns what it wrote until it stopped.</summary>
    public static Task<string> RunAsync(CancellationToken cancellationToken) =>
        RunAsync(environment: ReadOnlyDictionary<string, string>.Empty, cancellationToken);

    /// <summary>The outcome of the check with that name, as the host wrote it at startup (the detail of <c>/status</c> needs a session, #632).</summary>
    public static string StatusOf(string log, string check) => StartupCheckLog.OutcomeOf(log, check);

    private static async Task<string> RunAsync(IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken)
    {
        var baseAddress = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(baseAddress) with { Environment = environment };
        using var host = await HostProcess.StartAsync(installation, baseAddress, HostProcess.StartupTimeout, cancellationToken);
        using var response = await host.GetAsync("/status", cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var streams = await host.StopWithErrorAsync();
        return streams.Output + streams.Error;
    }
}
