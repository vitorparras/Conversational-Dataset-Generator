namespace Core.Host.Endpoints;

/// <summary>Answers whether the process is alive.</summary>
internal static class StatusEndpoint
{
    public const string Route = "/status";

    public static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, Handle);

    private static StatusResponse Handle() => new(Alive: true);
}
