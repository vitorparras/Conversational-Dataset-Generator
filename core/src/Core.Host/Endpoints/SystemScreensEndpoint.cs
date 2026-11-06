using Core.Boundary.Http;
using Core.Host.Screens;
using Core.Persistence.People;
using Core.Platform.Configuration;
using Core.Platform.Versions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The screens of the administration of the system, which read and change nothing: the configuration in force (#603) and the state of the system (#604).
/// Each one shows what its route of JSON returns, from the same call, and is reached by the technical administration and the audit, with an entry in the menu.
/// </summary>
internal static class SystemScreensEndpoint
{
    private const int StatePlace = 90;
    private const int ConfigurationPlace = 91;

    internal static void Map(IEndpointRouteBuilder endpoints, RecognizerKind recognizer)
    {
        _ = endpoints.MapGet(
                ScreenPaths.State,
                (HttpContext context, [AsParameters] StatusSources sources, [FromServices] VersionReport versions, [FromServices] IServiceVersionReader service) =>
                    StateAsync(context, SystemState.Status(sources, recognizer), versions, service))
            .WithMetadata(ScreenAccess.Of(SystemStateScreen.Title, StatePlace, PersonFunctions.Administration, PersonFunctions.Audit));
        _ = endpoints.MapGet(ScreenPaths.Configuration, InForce)
            .WithMetadata(ScreenAccess.Of(ConfigurationScreen.Title, ConfigurationPlace, PersonFunctions.Administration, PersonFunctions.Audit));
    }

    private static async Task<RazorComponentResult<ScreenFrame>> StateAsync(HttpContext context, StatusResponse status, VersionReport versions, IServiceVersionReader service)
    {
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(SystemStateScreen.State)] = status,
            [nameof(SystemStateScreen.Versions)] = await SystemState.VersionsAsync(versions, service, context.RequestAborted),
        };
        return ScreenResult.Of(context, typeof(SystemStateScreen), SystemStateScreen.Title, screen);
    }

    private static RazorComponentResult<ScreenFrame> InForce(HttpContext context, [FromServices] EffectiveConfiguration configuration) =>
        ScreenResult.Of(
            context, typeof(ConfigurationScreen), ConfigurationScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(ConfigurationScreen.Entries)] = configuration.Entries });
}
