using Core.Platform.Startup;

namespace Core.Host.Endpoints;

/// <summary>One dependency checked at startup, as the status shows it.</summary>
internal sealed record StatusCheckResponse(string Name, string Status, string Message);

/// <summary>Whether the process is alive, whether it is ready, and what it checked to decide.</summary>
internal sealed record StatusResponse(bool Alive, bool Ready, IReadOnlyList<StatusCheckResponse> Checks);

/// <summary>Answers the two separate questions of issue 24 CA-2: alive, and ready to serve.</summary>
internal static class StatusEndpoint
{
    public const string Route = "/status";

    /// <summary>Alive whenever it answers; ready only when every check passed.</summary>
    public static StatusResponse Handle(StartupReport report) =>
        new(
            Alive: true,
            Ready: report.AllPassed,
            Checks: [.. report.Results.Select(result => new StatusCheckResponse(
                result.Name,
                result.Status == CheckStatus.Passed ? "passed" : "failed",
                result.Message))]);

    public static void Map(IEndpointRouteBuilder endpoints, StartupReport report) =>
        endpoints.MapGet(Route, () => Handle(report));

    /// <summary>
    /// The report is read on each request, so a dependency that fails after startup shows at once:
    /// the process stays alive and stops being ready.
    /// </summary>
    public static void Map(IEndpointRouteBuilder endpoints, Func<StartupReport> report) =>
        endpoints.MapGet(Route, () => Handle(report()));
}
