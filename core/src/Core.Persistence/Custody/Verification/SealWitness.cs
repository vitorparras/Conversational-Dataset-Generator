namespace Core.Persistence.Custody.Verification;

/// <summary>Watches the events go by for the one at the sequence a seal counts, and keeps nothing but its digest.</summary>
internal sealed class SealWitness
{
    private string? found;

    /// <summary>The seal the chain is compared with.</summary>
    internal required ChainSeal Sealed { get; init; }

    internal void Accept(StoredEvent stored)
    {
        if (stored.Sequence == Sealed.Events)
        {
            found ??= Convert.ToHexStringLower(stored.Digest);
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
