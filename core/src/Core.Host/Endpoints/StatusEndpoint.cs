namespace Core.Host.Endpoints;

/// <summary>The state the process reports about itself.</summary>
internal sealed record StatusResponse(bool Alive);

/// <summary>Answers whether the process is alive.</summary>
internal static class StatusEndpoint
{
    public const string Route = "/status";

    public static StatusResponse Handle() => new(Alive: true);

    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(Route, Handle);
}
