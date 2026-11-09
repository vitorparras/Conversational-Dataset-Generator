namespace Core.Persistence.Content;

/// <summary>One occurrence found in the text of an item and the destination chosen for it, with the offsets in code points as they crossed the boundary, never converted again.</summary>
public sealed record ItemOccurrence
{
    /// <summary>The occurrence, with its half-open span in code points, the kind the recognizer named and what becomes of it.</summary>
    /// <param name="start">The code point offset where the occurrence starts.</param>
    /// <param name="end">The code point offset just past the occurrence.</param>
    /// <param name="kind">What the occurrence is, as the recognizer names it.</param>
    /// <param name="destination">What becomes of it.</param>
    internal ItemOccurrence(int start, int end, string kind, OccurrenceDestination destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        ArgumentNullException.ThrowIfNull(kind);
        if (!Enum.IsDefined(destination))
        {
            throw new ArgumentOutOfRangeException(nameof(destination), destination, "The destination is not one of the three.");
        }

        Start = start;
        End = end;
        Kind = kind;
        Destination = destination;
    }

    /// <summary>The code point offset where the occurrence starts.</summary>
    public int Start { get; }

    /// <summary>The code point offset just past the occurrence.</summary>
    public int End { get; }

    /// <summary>What the occurrence is, as the recognizer names it.</summary>
    public string Kind { get; }

    /// <summary>What becomes of it.</summary>
    public OccurrenceDestination Destination { get; }

    /// <summary>The kind and the destination; never the offsets, which with the text rebuild what was found.</summary>
    public override string ToString() => $"ItemOccurrence {{ Kind = {Kind}, Destination = {Destination} }}";
}
