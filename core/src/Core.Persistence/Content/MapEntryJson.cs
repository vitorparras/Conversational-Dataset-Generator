using System.Text.Json;
using Core.Persistence.Content.Writing;

namespace Core.Persistence.Content;

/// <summary>How the entries of the map of an item are written before they are sealed, and read once opened: the marker, the original and whether it stands for an author.</summary>
internal static class MapEntryJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    internal static string Write(IEnumerable<NewMapEntry> entries) =>
        JsonSerializer.Serialize(entries.Select(entry => new Row(entry.Marker, entry.Original, entry.Author)).ToList(), Options);

    internal static List<NewMapEntry> Read(string entries) =>
        [.. (JsonSerializer.Deserialize<List<Row>>(entries, Options) ?? []).Select(row => new NewMapEntry(row.Marker, row.Original, row.Author, OccurrenceDestination.Pseudonymize))];

    private sealed record Row(string Marker, string Original, bool Author);
}
