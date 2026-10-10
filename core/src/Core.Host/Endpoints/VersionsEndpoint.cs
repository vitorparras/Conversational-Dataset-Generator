using System.Text.Json.Serialization;
using Core.Platform.Versioning;

namespace Core.Host.Endpoints;

/// <summary>
/// One version as the endpoint shows it: <c>{"state":"known","value":"..."}</c> or
/// <c>{"state":"absent"}</c>. Absent is a state of its own, never null or an empty string.
/// </summary>
internal sealed record VersionFieldResponse(
    string State,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Value);

/// <summary>Answers under which versions the system runs (issue 51).</summary>
internal static class VersionsEndpoint
{
    public const string Route = "/versions";

    public static IReadOnlyDictionary<string, VersionFieldResponse> Handle(VersionReport report) =>
        report.Fields.ToDictionary(
            field => field.Name,
            field => field.Value switch
            {
                Known known => new VersionFieldResponse("known", known.Value),
                Absent => new VersionFieldResponse("absent", null),
                _ => throw new InvalidOperationException($"unknown version value {field.Value.GetType()}"),
            });

    public static void Map(IEndpointRouteBuilder endpoints, VersionReport report) =>
        endpoints.MapGet(Route, () => Handle(report));
}
