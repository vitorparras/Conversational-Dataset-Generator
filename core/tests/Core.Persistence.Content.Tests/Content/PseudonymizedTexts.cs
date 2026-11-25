using System.Security.Cryptography;
using Core.Persistence.Content;
using Core.Persistence.Content.Writing;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Content;

/// <summary>The texts of a pseudonymized item for the tests of the map and of the markers (issue 101): synthetic originals, and markers drawn at random so that no two tests share one.</summary>
internal static class PseudonymizedTexts
{
    public const string Raw = "texto bruto sintetico zqx";

    public const string Sanitized = "texto sanitizado sintetico zqx";

    /// <summary>Markers of a person, distinct among themselves, drawn as the swap draws them.</summary>
    /// <param name="count">How many.</param>
    public static string[] Draw(int count)
    {
        var drawn = new HashSet<string>(StringComparer.Ordinal);
        while (drawn.Count < count)
        {
            _ = drawn.Add($"PESSOA_{RandomNumberGenerator.GetInt32(100_000_000):D8}");
        }

        return [.. drawn];
    }

    /// <summary>The original a marker replaced in the tests: synthetic, and different for each marker, so that a dump that held it would show it.</summary>
    /// <param name="marker">The marker.</param>
    public static string OriginalOf(string marker) => $"Zqxnome Sintetico {marker[^4..]}";

    /// <summary>An item whose pseudonymized text holds the markers given, each an occurrence to pseudonymize of no author, with the map that reverses them.</summary>
    /// <param name="markers">The markers.</param>
    public static NewItemText Holding(params string[] markers) =>
        WithMap([.. markers.Select(marker => new NewMapEntry(marker, OriginalOf(marker), false, OccurrenceDestination.Pseudonymize))]);

    /// <summary>An item whose pseudonymized text holds the markers of the map given, with that map.</summary>
    /// <param name="map">The entries of the map.</param>
    public static NewItemText WithMap(IReadOnlyList<NewMapEntry> map) =>
        ItemTexts.Of(Raw, Sanitized, PseudonymizedOf(map.Select(entry => entry.Marker))) with { Map = map };

    /// <summary>The pseudonymized text of an item that holds the markers given.</summary>
    /// <param name="markers">The markers.</param>
    public static string PseudonymizedOf(IEnumerable<string> markers) => $"{string.Join(" e ", markers)} disseram algo sintetico zqx";
}
