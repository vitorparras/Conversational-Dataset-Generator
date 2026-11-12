using Core.Ingestion.Deciding;
using Core.Persistence.Content;
using Core.Persistence.Content.Writing;

namespace Core.Host.Submissions;

/// <summary>
/// The one place where the texts of an item are built for the writer: from the material the destinations were decided for, and from nothing else. The writer stores what it is given and
/// decides nothing, and the constructors of what it stores are the host's alone, so that no item is recorded as clean by a caller that did not analyse its text (issue 84, CA-1).
/// </summary>
internal static class DecidedItemTexts
{
    /// <summary>The texts of the item that is the text of the material at the position given.</summary>
    /// <param name="material">The material with the destination decided for every occurrence of every text.</param>
    /// <param name="text">The position of the text in the material.</param>
    /// <param name="raw">The text as it arrived.</param>
    internal static NewItemText Of(DecidedMaterial material, int text, string raw)
    {
        ArgumentNullException.ThrowIfNull(material);
        var stored = material.Of(text).Select(Stored).ToList();
        return new NewItemText(raw, null, null, stored);
    }

    private static ItemOccurrence Stored(DecidedOccurrence decided) =>
        new(decided.Occurrence.Start, decided.Occurrence.End, decided.Occurrence.Kind, Stored(decided.Destination));

    private static OccurrenceDestination Stored(Destination destination) => destination switch
    {
        Destination.Remove => OccurrenceDestination.Remove,
        Destination.Pseudonymize => OccurrenceDestination.Pseudonymize,
        Destination.Preserve => OccurrenceDestination.Preserve,
        _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, "The destination is not one of the three."),
    };
}
