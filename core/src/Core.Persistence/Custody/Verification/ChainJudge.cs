namespace Core.Persistence.Custody;

/// <summary>
/// Judges the events of the chain one after another, in the order of their sequence: each must come right after the one before, point to its digest and hold the digest
/// the chain declares for it. It keeps the first that does not, goes on counting what it is given, and keeps nothing else of the events it saw but the last digest.
/// </summary>
internal sealed class ChainJudge
{
    private byte[] previousDigest = ChainDigest.Origin();
    private long expectedSequence = 1;

    /// <summary>How many events were given, the inconsistent one and those after it included.</summary>
    internal long Read { get; private set; }

    /// <summary>The first event that does not hold; null while every event given does.</summary>
    internal InconsistentEvent? First { get; private set; }

    internal void Accept(StoredEvent stored)
    {
        Read++;
        if (First is not null)
        {
            return;
        }

        First = Inconsistency(stored) is { } kind ? new InconsistentEvent(stored.Sequence, kind) : null;
        previousDigest = stored.Digest;
        expectedSequence = stored.Sequence + 1;
    }

    private ChainInconsistency? Inconsistency(StoredEvent stored)
    {
        if (stored.Sequence != expectedSequence)
        {
            return ChainInconsistency.SequenceGap;
        }

        if (!stored.PreviousDigest.AsSpan().SequenceEqual(previousDigest))
        {
            return ChainInconsistency.PreviousDigestMismatch;
        }

        return stored.Digest.AsSpan().SequenceEqual(ChainDigest.Of(stored, previousDigest)) ? null : ChainInconsistency.DigestMismatch;
    }
}
