using Core.Boundary.Contracts;

namespace Core.Ingestion.Deciding;

/// <summary>Either the item with a destination for every occurrence, or the reason the service gave no analysis; never both, never neither.</summary>
public sealed record DestinationResult
{
    private DestinationResult(DecidedMaterial? decided, UnavailabilityReason? reason)
    {
        Decided = decided;
        Reason = reason;
    }

    /// <summary>The item with its destinations; null when a text was not analysed.</summary>
    public DecidedMaterial? Decided { get; }

    /// <summary>Why a text was not analysed, as the boundary names it; null when the item was decided.</summary>
    public UnavailabilityReason? Reason { get; }

    /// <summary>Every text was analysed, and every occurrence found has its destination.</summary>
    internal static DestinationResult Of(DecidedMaterial decided)
    {
        ArgumentNullException.ThrowIfNull(decided);
        return new(decided, null);
    }

    /// <summary>A text was not analysed, for the reason given, and no destination is guessed in its place.</summary>
    internal static DestinationResult Unavailable(UnavailabilityReason reason) => new(null, reason);
}
