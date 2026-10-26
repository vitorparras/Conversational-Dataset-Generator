using Core.Boundary.Http;
using Core.Platform.Versions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>Answers under which versions the process runs, with every version it could not obtain marked absent; the model version is asked of the service on each request.</summary>
internal static class VersionsEndpoint
{
    internal const string Route = "/versions";

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, InForceAsync);

    private static async Task<VersionReport> InForceAsync(
        [FromServices] VersionReport versions, [FromServices] IModelVersionReader model, CancellationToken cancellationToken) =>
        versions.WithModelVersion(await model.ReadAsync(cancellationToken));
}
