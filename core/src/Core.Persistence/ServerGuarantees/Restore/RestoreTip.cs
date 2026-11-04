namespace Core.Persistence.ServerGuarantees.Restore;

/// <summary>The tip of the chain noted outside the copy (005): the sequence of the last event and its digest in hexadecimal.</summary>
public sealed record RestoreTip(long Sequence, string Digest)
{
    /// <summary>Whether the other tip is the same event.</summary>
    public bool Matches(RestoreTip? other) => other is not null && other.Sequence == Sequence && string.Equals(other.Digest, Digest, StringComparison.OrdinalIgnoreCase);
}
