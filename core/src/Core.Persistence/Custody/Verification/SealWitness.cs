namespace Core.Persistence.Custody.Verification;

/// <summary>Watches the events go by for the one at the sequence a seal counts, and keeps nothing but its digest, recomputed here and not read from the column that holds it.</summary>
internal sealed class SealWitness
{
    private string? found;

    /// <summary>The seal the chain is compared with.</summary>
    internal required ChainSeal Sealed { get; init; }

    /// <summary>
    /// The digest kept is the one the event deserves given the digest before it, so that an event altered without touching its column of digest is not the sealed one: the column
    /// is what an alteration of the base would leave in place, and the verification must not ask it whether it is right.
    /// </summary>
    internal void Accept(StoredEvent stored, ReadOnlySpan<byte> previous)
    {
        if (stored.Sequence == Sealed.Events && found is null)
        {
            found = Convert.ToHexStringLower(ChainDigest.Of(stored, previous));
        }
    }

    internal SealComparison Compare()
    {
        var seen = found ?? (Sealed.Events == 0 ? Convert.ToHexStringLower(ChainDigest.Origin()) : null);
        var outcome = seen switch
        {
            null => SealOutcome.CountBelowSealed,
            _ when string.Equals(seen, Sealed.Digest, StringComparison.Ordinal) => SealOutcome.Holds,
            _ => SealOutcome.DigestDiffers,
        };
        return new SealComparison { Sealed = Sealed, Outcome = outcome };
    }
}
