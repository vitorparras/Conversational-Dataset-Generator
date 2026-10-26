using Core.Boundary.Http;
using Core.Platform.Versions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>Answers under which versions the process runs, absent where it could not obtain one; the service's own three are asked of it on each request.</summary>
internal static class VersionsEndpoint
{
    internal const string Route = "/versions";

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, InForceAsync);

    private static async Task<VersionReport> InForceAsync(
        [FromServices] VersionReport versions, [FromServices] IServiceVersionReader service, CancellationToken cancellationToken) =>
        versions.WithServiceVersions(await service.ReadAsync(cancellationToken));
}
