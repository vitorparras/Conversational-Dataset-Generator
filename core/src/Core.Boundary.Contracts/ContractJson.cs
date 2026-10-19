using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Boundary.Contracts;

/// <summary>How the messages of the contract are written and read: the one place their JSON form is declared.</summary>
public static class ContractJson
{
    /// <summary>Snake case names, as the language service writes them; an unknown, missing, null or repeated field is an error.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true,
            AllowDuplicateProperties = false,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
