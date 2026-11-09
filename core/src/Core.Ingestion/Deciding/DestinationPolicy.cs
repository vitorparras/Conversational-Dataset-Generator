using Core.Boundary.Contracts;

namespace Core.Ingestion.Deciding;

/// <summary>
/// What becomes of each occurrence, decided once, from the occurrence alone. A secret is always removed. The kind says whether a name is of a person or of a place, and the grammar breaks
/// the tie: only a proper noun is a name, pseudonymized when it is of a person and preserved when it is of a place, and what is not a name has no relation to keep and is removed.
/// Whatever the policy does not recognise is removed, so that a kind it never heard of cannot pass as clean (decision 0018).
/// </summary>
public static class DestinationPolicy
{
    private const string ProperNoun = "PROPN";

    /// <summary>The one destination of the occurrence.</summary>
    /// <param name="occurrence">What the service found, with its kind and the part of speech of its head.</param>
    public static Destination Of(Occurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        if (OccurrenceKinds.IsSecret(occurrence.Kind))
        {
            return Destination.Remove;
        }

        var name = string.Equals(occurrence.PartOfSpeech.Trim(), ProperNoun, StringComparison.OrdinalIgnoreCase);
        return NameDestination(occurrence.Kind, name);
    }

    private static Destination NameDestination(string kind, bool name)
    {
        var ofPerson = string.Equals(kind, OccurrenceKinds.Person, StringComparison.Ordinal) || string.Equals(kind, OccurrenceKinds.Organization, StringComparison.Ordinal);
        var ofPlace = string.Equals(kind, OccurrenceKinds.Location, StringComparison.Ordinal);
        if (!name || (!ofPerson && !ofPlace))
        {
            return Destination.Remove;
        }

        return ofPerson ? Destination.Pseudonymize : Destination.Preserve;
    }
}
