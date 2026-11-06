namespace Core.Host.Endpoints;

/// <summary>Answers whether the process is alive and whether it is ready, with the recognizer in force, the state of the language service it asks and the result of each startup check.</summary>
internal static class StatusEndpoint
{
    internal const string Route = "/status";

    internal static void Map(IEndpointRouteBuilder endpoints, RecognizerKind recognizer) =>
        _ = endpoints.MapGet(
            Route,
            ([AsParameters] StatusSources sources) => SystemState.Status(sources, recognizer)).WithMetadata(new SessionOpen());
}
