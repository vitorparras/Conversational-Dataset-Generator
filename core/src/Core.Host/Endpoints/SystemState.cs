using Core.Boundary.Http;
using Core.Host.Supervision;
using Core.Platform.Versions;

namespace Core.Host.Endpoints;

/// <summary>
/// What the host says about itself, read once for every way of showing it: the routes <c>/status</c> and <c>/versions</c> and the screen of the state of the system answer with
/// what these two calls return, so that no rule of what is alive, ready or in force is written twice. The configuration in force is the singleton
/// <c>Core.Platform.Configuration.EffectiveConfiguration</c> (every key, its origin, a sensitive value redacted), which the route <c>/configuration</c> returns as it is.
/// </summary>
internal static class SystemState
{
    /// <summary>
    /// Alive is always true when the process answers; ready only when every check passed and, with the http recognizer, the language service is available. The fake asks no service,
    /// so the watch, which no start ever moves, says nothing about it and the state of the service is null.
    /// </summary>
    internal static StatusResponse Status(StatusSources sources, RecognizerKind recognizer)
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

    /// <summary>The versions the process runs under, absent where it could not obtain one; the three of the service are asked of it on each call.</summary>
    internal static async Task<VersionReport> VersionsAsync(VersionReport versions, IServiceVersionReader service, CancellationToken cancellationToken) =>
        versions.WithServiceVersions(await service.ReadAsync(cancellationToken));
}
