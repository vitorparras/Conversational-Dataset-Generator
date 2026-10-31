using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;

namespace Core.Host;

/// <summary>The JSON the routes speak: enums by name, and a field unknown, missing or null refused.</summary>
internal static class HostJson
{
    internal static void Strict(JsonOptions options)
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.SerializerOptions.RespectRequiredConstructorParameters = true;
        options.SerializerOptions.RespectNullableAnnotations = true;
    }
}
