using Core.Boundary.Contracts;

namespace Core.Ingestion.Deciding;

/// <summary>An occurrence and the destination chosen for it, kept as the pair so that nothing later asks the policy again.</summary>
/// <param name="Occurrence">What the service found, with its offsets in code points, exactly as it came across the boundary.</param>
/// <param name="Destination">What becomes of it.</param>
public sealed record DecidedOccurrence(Occurrence Occurrence, Destination Destination)
{
    /// <summary>The kind and the destination; never the offsets, which with the text rebuild what was found.</summary>
    public override string ToString() => $"DecidedOccurrence {{ Kind = {Occurrence.Kind}, Destination = {Destination} }}";
}
