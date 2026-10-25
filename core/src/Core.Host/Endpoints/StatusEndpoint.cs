using Core.Host.Supervision;
using Core.Platform.Startup;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>Answers whether the process is alive and whether it is ready, with the recognizer in force, the state of the language service it asks and the result of each startup check.</summary>
internal static class StatusEndpoint
{
    internal const string Route = "/status";

    internal static void Map(IEndpointRouteBuilder endpoints, RecognizerKind recognizer) =>
        _ = endpoints.MapGet(
            Route, ([FromServices] StartupReport report, [FromServices] ServiceProcessWatch watch) => Describe(report, watch, recognizer));

    /// <summary>The fake asks no service, so the watch, which no start ever moves, says nothing about it.</summary>
    private static StatusResponse Describe(StartupReport report, ServiceProcessWatch watch, RecognizerKind recognizer)
    {
        ServiceState? service = recognizer == RecognizerKind.Http ? watch.State : null;
        return new(
            Alive: true, Ready: report.Ready && service is null or ServiceState.Available, recognizer, service, report.Results);
    }
}
