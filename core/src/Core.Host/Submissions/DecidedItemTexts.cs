using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;
using Core.Ingestion.Pseudonymizing;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Content.Writing;

namespace Core.Host.Submissions;

/// <summary>
/// The one place where the texts of an item are built for the writer: from the material the destinations were decided for, and from nothing else. The writer stores what it is given and
/// decides nothing, and the constructors of what it stores are the host's alone, so that no item is recorded as clean by a caller that did not analyse its text (issue 84, CA-1). The rerun
/// builds them again from the raw text and the destinations decided when the item came in, which are not decided again (issue 97).
/// </summary>
internal static class DecidedItemTexts
{
    /// <summary>The texts of the item that is the text of the material at the position given, with what the sanitization left of it.</summary>
    /// <param name="material">The material with the destination decided for every occurrence of every text.</param>
    /// <param name="text">The position of the text in the material.</param>
    /// <param name="raw">The text as it arrived.</param>
    /// <param name="clean">The sanitized text, its trail and the report of each step.</param>
    internal static NewItemText Of(DecidedMaterial material, int text, string raw, SanitizedText clean)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(clean);
        var stored = material.Of(text).Select(Stored).ToList();
        return new NewItemText(raw, null, stored) { Sanitization = Stored(clean) };
    }

    /// <summary>The texts of the item as <see cref="Of"/> gives them, with the text the swap made of the sanitized one and an entry of the map for each marker and original it holds.</summary>
    /// <param name="material">The material with the destination decided for every occurrence of every text.</param>
    /// <param name="text">The position of the text in the material.</param>
    /// <param name="raw">The text as it arrived.</param>
    /// <param name="item">The sanitized text, and what the swap made of it.</param>
    internal static NewItemText Swapped(DecidedMaterial material, int text, string raw, (SanitizedText Clean, Pseudonymized Swap) item)
    {
        ArgumentNullException.ThrowIfNull(item.Swap);
        var plain = Of(material, text, raw, item.Clean);
        return new NewItemText(raw, item.Swap.Text, plain.Occurrences!)
        {
            Sanitization = plain.Sanitization,
            Map = [.. item.Swap.Entries.Select(entry => new NewMapEntry(entry.Marker.Text, entry.Original, entry.IsAuthor, OccurrenceDestination.Pseudonymize))],
        };
    }

    /// <summary>
    /// What the rerun of the steps in force makes of what the item holds, or none when the rerun refused an item whose destinations were never decided; the occurrences held keep no
    /// part of speech nor role, which no step reads. An item pseudonymized whose sanitized text changes is pseudonymized again by the swap, with the markers of its map.
    /// </summary>
    internal static Resanitized? Rerun(SanitizationRerun steps, PseudonymSwap swap, RerunSource held)
    {
        var decided = held.Occurrences?.Select(occurrence => new DecidedOccurrence(new Occurrence(occurrence.Start, occurrence.End, occurrence.Kind, "", ""), Decided(occurrence.Destination)));
        var produced = steps.Rerun(held.Raw, decided is null ? null : [.. decided], held.StoredText);
        if (produced.Result.Sanitized is not { } sanitized)
        {
            return null;
        }

        var stored = Stored(sanitized);
        var resanitized = new Resanitized(stored.Text, stored.Version, stored.Steps, stored.Trail, stored.Counts, stored.Untreated, produced.Identical);
        if (held.Map is null || produced.Identical)
        {
            return resanitized;
        }

        var (text, map) = RerunSwapping.Again(swap, held, sanitized);
        return resanitized with { Pseudonymized = text, Map = map };
    }

    private static ItemSanitization Stored(SanitizedText clean) => new()
    {
        Text = clean.Text,
        Version = clean.Version,
        Steps = string.Join("\n", clean.Order.Select(step => $"{step.Name}@{step.Version}")),
        Trail = TrailJson.Write(clean.Trail),
        Counts = [.. clean.Report.Select(step => step.Count)],
        Untreated = [.. clean.Report.Select(step => step.Untreated)],
    };

    private static ItemOccurrence Stored(DecidedOccurrence decided) =>
        new(decided.Occurrence.Start, decided.Occurrence.End, decided.Occurrence.Kind, Stored(decided.Destination));

    private static OccurrenceDestination Stored(Destination destination) => destination switch
    {
        Destination.Remove => OccurrenceDestination.Remove,
        Destination.Pseudonymize => OccurrenceDestination.Pseudonymize,
        Destination.Preserve => OccurrenceDestination.Preserve,
        _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, "The destination is not one of the three."),
    };

    private static Destination Decided(OccurrenceDestination destination) => destination switch
    {
        OccurrenceDestination.Remove => Destination.Remove,
        OccurrenceDestination.Pseudonymize => Destination.Pseudonymize,
        OccurrenceDestination.Preserve => Destination.Preserve,
        _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, "The destination is not one of the three."),
    };
}
