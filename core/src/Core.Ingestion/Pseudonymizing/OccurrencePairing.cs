using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;

namespace Core.Ingestion.Pseudonymizing;

/// <summary>
/// The group of an occurrence stored before the coreference (issue 102, F3): the answer for the raw text gives the groups and nothing else, so each occurrence stored keeps its
/// destination and takes the group of the occurrence of the answer in the same text, at the same positions and of the same kind. Its destination is not decided again.
/// </summary>
public static class OccurrencePairing
{
    /// <summary>The occurrences stored, in their order, each with the group of its pair; none when one of them has no pair, so that the item is refused.</summary>
    /// <param name="stored">The occurrences stored with the item, with their destinations.</param>
    /// <param name="answered">The occurrences the service gave for the texts asked together.</param>
    /// <param name="text">The position of the text of the item among the texts asked.</param>
    public static IReadOnlyList<DecidedOccurrence>? Paired(IReadOnlyList<DecidedOccurrence> stored, IReadOnlyList<GroupedOccurrence> answered, int text)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(answered);
        var groups = answered.Where(found => found.TextIndex == text).ToLookup(found => (found.Start, found.End, found.Kind), found => found.Group);
        var paired = new List<DecidedOccurrence>(stored.Count);
        foreach (var held in stored)
        {
            var key = (held.Occurrence.Start, held.Occurrence.End, held.Occurrence.Kind);
            if (!groups.Contains(key))
            {
                return null;
            }

            paired.Add(held with { Group = groups[key].First() });
        }

        return paired;
    }
}
