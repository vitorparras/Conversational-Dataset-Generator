using Core.Host.Supervision;

namespace Core.Host.Endpoints;

/// <summary>Answers whether the process is alive and whether it is ready, with the recognizer in force, the state of the language service it asks and the result of each startup check.</summary>
internal static class StatusEndpoint
{
    internal const string Route = "/status";

    internal static void Map(IEndpointRouteBuilder endpoints, RecognizerKind recognizer) =>
        _ = endpoints.MapGet(
            Route,
            ([AsParameters] StatusSources sources) => Describe(sources, recognizer)).WithMetadata(new SessionOpen());

    /// <summary>The fake asks no service, so the watch, which no start ever moves, says nothing about it.</summary>
    private static StatusResponse Describe(StatusSources sources, RecognizerKind recognizer)
    {
        var (report, watch, installation, comparison, sealWarning) = sources;
        ServiceState? service = recognizer == RecognizerKind.Http ? watch.State : null;
        return new(
            Alive: true,
            Ready: report.Ready && service is null or ServiceState.Available,
            Recognizer: recognizer,
            LanguageService: service,
            CheckedAt: report.CheckedAt,
            Checks: report.Results,
            Installation: installation,
            AuditComparison: comparison.Current,
            SealWarning: sealWarning.Current);
    }
}
