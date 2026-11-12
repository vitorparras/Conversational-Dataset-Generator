using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Persistence.Content;

/// <summary>How the occurrences of an item are written before they are sealed and read after they are opened: the one place their form is declared.</summary>
internal static class OccurrenceJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
    };

    internal static string Write(IEnumerable<ItemOccurrence> occurrences)
    {
        var rows = occurrences.Select(Written).ToList();
        return JsonSerializer.Serialize(rows, Options);
    }

    /// <exception cref="InvalidDataException">What was sealed is not a list of occurrences; the seal authenticates it, so this is a defect and not a case.</exception>
    internal static List<ItemOccurrence> Read(string json)
    {
        var rows = JsonSerializer.Deserialize<List<Row>>(json, Options) ?? throw new InvalidDataException("The sealed occurrences are not a list.");
        var occurrences = rows.Select(Occurrence).ToList();
        return occurrences;
    }

    private static Row Written(ItemOccurrence occurrence) => new(occurrence.Start, occurrence.End, occurrence.Kind, occurrence.Destination.ToString());

    private static ItemOccurrence Occurrence(Row row) =>
        Enum.TryParse<OccurrenceDestination>(row.Destination, ignoreCase: false, out var destination)
            ? new ItemOccurrence(row.Start, row.End, row.Kind, destination)
            : throw new InvalidDataException("The sealed occurrences name a destination that is not one of the three.");

    private sealed record Row(int Start, int End, string Kind, string Destination);
}
