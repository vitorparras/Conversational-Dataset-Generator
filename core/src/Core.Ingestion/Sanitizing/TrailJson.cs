using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// How the trail is written before it is sealed with the item and read after it is opened: the one place its form is declared, the length of the raw text and the edits of
/// each step.
/// </summary>
public static class TrailJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        Converters = { new JsonStringEnumConverter<SanitizationMarker>(allowIntegerValues: false) },
    };

    /// <summary>The trail as text, to be sealed with the item.</summary>
    /// <param name="trail">The trail the sanitization left.</param>
    public static string Write(SanitizationTrail trail)
    {
        ArgumentNullException.ThrowIfNull(trail);
        return JsonSerializer.Serialize(new Row(trail.RawLength, trail.Steps), Options);
    }

    /// <summary>The trail rebuilt from its text, checked against the text each step received.</summary>
    /// <param name="json">The trail as <see cref="Write" /> wrote it.</param>
    /// <exception cref="InvalidDataException">The text is not a trail whose edits fit the texts; the seal authenticates it, so this is a defect and not a case.</exception>
    public static SanitizationTrail Read(string json)
    {
        try
        {
            var row = JsonSerializer.Deserialize<Row>(json, Options) ?? throw new InvalidDataException("The sealed trail is empty.");
            var steps = new List<TrailStep>(row.Steps.Count);
            ArgumentOutOfRangeException.ThrowIfNegative(row.Raw);
            var length = row.Raw;
            foreach (var edits in row.Steps)
            {
                steps.Add(TrailStep.Of(length, edits));
                length = steps[^1].OutputLength;
            }

            return new SanitizationTrail(row.Raw, steps);
        }
        catch (Exception defect) when (defect is JsonException or ArgumentException)
        {
            throw new InvalidDataException("The sealed trail is not a trail whose edits fit the texts.", defect);
        }
    }

    private sealed record Row(int Raw, IReadOnlyList<IReadOnlyList<SanitizationEdit>> Steps);
}
