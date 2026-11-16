using Core.Persistence.People;
using Core.Platform.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>Shows the configuration in force: every key, its origin, and sensitive values redacted.</summary>
internal static class ConfigurationEndpoint
{
    internal const string Route = "/configuration";

    /// <summary>The functions that reach the screen of the configuration (policy 006): the route of JSON shows the same.</summary>
    private static readonly ScreenAccess Access = ScreenAccess.Unlisted(PersonFunctions.Administration, PersonFunctions.Audit);

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, InForce).WithMetadata(Access);

    private static EffectiveConfiguration InForce([FromServices] EffectiveConfiguration configuration) => configuration;
}
