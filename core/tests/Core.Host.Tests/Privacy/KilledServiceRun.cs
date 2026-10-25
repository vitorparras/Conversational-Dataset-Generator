namespace Core.Host.Tests.Privacy;

/// <summary>
/// One run of the host with the real service, played once for all the tests of the error path, since starting the service
/// is what costs: the service is killed by its process number, the analysis is refused, a retry names it, and a body the
/// route does not know is rejected. The host is gone when the run is handed over.
/// </summary>
internal static class KilledServiceRun
{
    private static readonly Lazy<Task<KilledServiceOutcome>> Played = new(PlayAsync);

    public static Task<KilledServiceOutcome> OutcomeAsync() => Played.Value;

    private static async Task<KilledServiceOutcome> PlayAsync()
    {
        using var host = await LoggedHost.StartAsync(realService: true, CancellationToken.None);
        var refused = await host.AskOnceTheServiceIsDeadAsync(CancellationToken.None);
        var retried = await host.AskAsync(host.Marker, refused.Reference, CancellationToken.None);
        var rejected = await host.RejectedAsync(host.Marker, CancellationToken.None);
        var evidence = await host.StopAsync();
        return new KilledServiceOutcome(
            refused.Status,
            retried.Status,
            rejected,
            refused.Reference ?? string.Empty,
            host.LogText,
            evidence.Stdout,
            evidence.Stderr,
            LogSweep.Leaks(evidence));
    }
}
