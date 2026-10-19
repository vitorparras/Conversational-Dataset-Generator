using Core.Platform.Versions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>Answers under which versions the process runs, with every version it could not obtain marked absent.</summary>
internal static class VersionsEndpoint
{
    internal const string Route = "/versions";

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, InForce);

    private static VersionReport InForce([FromServices] VersionReport versions) => versions;
}
