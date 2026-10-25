using System.Text.Json;
using Core.Boundary.Contracts;

namespace Core.Boundary.Http;

/// <summary>What the version route of the language service answers; the recognizer uses the contract version and the version report the model version.</summary>
/// <param name="ServiceVersion">The version of the service.</param>
/// <param name="ContractVersion">The version of the contract the service speaks.</param>
/// <param name="ModelVersion">The version of the model behind the service; null when there is no model.</param>
public sealed record VersionAnswer(string ServiceVersion, string ContractVersion, string? ModelVersion)
{
    /// <summary>The contract version the body declares; null when the body is not a version answer.</summary>
    internal static string? ContractVersionIn(ReadOnlyMemory<byte> body) => Read(body)?.ContractVersion;

    /// <summary>The model version the body declares; null when the body is not a version answer or declares none.</summary>
    internal static string? ModelVersionIn(ReadOnlyMemory<byte> body) => Read(body)?.ModelVersion;

    private static VersionAnswer? Read(ReadOnlyMemory<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<VersionAnswer>(body.Span, ContractJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
