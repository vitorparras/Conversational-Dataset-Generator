using Core.Platform.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>Shows the configuration in force: every key, its origin, and sensitive values redacted.</summary>
internal static class ConfigurationEndpoint
{
    internal const string Route = "/configuration";

    /// <summary>Reached by the functions of the screen of the configuration (policy 006), declared once in <see cref="SystemScreensEndpoint"/>.</summary>
    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, InForce).WithMetadata(SystemScreensEndpoint.Access);

    private static EffectiveConfiguration InForce([FromServices] EffectiveConfiguration configuration) => configuration;
}
