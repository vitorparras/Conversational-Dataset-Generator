using Core.Platform.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>Shows the configuration in force: every key, its origin, and sensitive values redacted.</summary>
internal static class ConfigurationEndpoint
{
    internal const string Route = "/configuration";

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, InForce);

    private static EffectiveConfiguration InForce([FromServices] EffectiveConfiguration configuration) => configuration;
}
