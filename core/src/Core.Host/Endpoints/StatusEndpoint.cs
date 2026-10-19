using Core.Platform.Startup;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>Answers whether the process is alive and whether it is ready, with the result of each startup check.</summary>
internal static class StatusEndpoint
{
    internal const string Route = "/status";

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, Describe);

    private static StatusResponse Describe([FromServices] StartupReport report) =>
        new(Alive: true, Ready: report.Ready, Checks: report.Results);
}
