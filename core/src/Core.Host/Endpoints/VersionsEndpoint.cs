using Core.Boundary.Http;
using Core.Persistence.People;
using Core.Platform.Versions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>Answers under which versions the process runs, absent where it could not obtain one; the service's own three are asked of it on each request.</summary>
internal static class VersionsEndpoint
{
    internal const string Route = "/versions";

    /// <summary>The functions that reach the screen of the state of the system (policy 006): the route of JSON shows the same.</summary>
    private static readonly ScreenAccess Access = ScreenAccess.Unlisted(PersonFunctions.Administration, PersonFunctions.Audit);

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, InForceAsync).WithMetadata(Access);

    private static Task<VersionReport> InForceAsync(
        [FromServices] VersionReport versions, [FromServices] IServiceVersionReader service, CancellationToken cancellationToken) =>
        SystemState.VersionsAsync(versions, service, cancellationToken);
}
