using Core.Boundary.Contracts;

namespace Core.Ingestion.Pseudonymizing;

/// <summary>The kind of marker of each kind of occurrence that names, and the prefix the marker of each kind is written with.</summary>
public static class MarkerKinds
{
    /// <summary>The kind of marker of an occurrence of the kind given; none for a kind that does not name a person, a place or an organization.</summary>
    /// <param name="occurrenceKind">The kind the recognizer gave the occurrence.</param>
    public static MarkerKind? Of(string occurrenceKind)
    {
        ArgumentNullException.ThrowIfNull(occurrenceKind);
        if (string.Equals(occurrenceKind, OccurrenceKinds.Person, StringComparison.Ordinal))
        {
            return MarkerKind.Person;
        }

        if (string.Equals(occurrenceKind, OccurrenceKinds.Location, StringComparison.Ordinal))
        {
            return MarkerKind.Place;
        }

        return string.Equals(occurrenceKind, OccurrenceKinds.Organization, StringComparison.Ordinal) ? MarkerKind.Organization : null;
    }

    /// <summary>The prefix a marker of the kind is written with.</summary>
    /// <param name="kind">The kind of marker.</param>
    public static string Prefix(MarkerKind kind) => kind switch
    {
        MarkerKind.Person => "PESSOA",
        MarkerKind.Place => "LUGAR",
        MarkerKind.Organization => "ORGANIZACAO",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind of marker is not known."),
    };
}
