namespace Core.Persistence.Custody.Verification;

/// <summary>How the chain stands against a seal; the two ways it does not are told apart.</summary>
public enum SealOutcome
{
    /// <summary>The chain has the event the seal counts, and its digest is the sealed one; the chain may have grown since.</summary>
    Holds,

    /// <summary>The chain has no event at the sequence the seal counts: the last events were taken away, or the base is a shorter one.</summary>
    CountBelowSealed,

    /// <summary>The event at the sequence the seal counts has another digest: the base is another, coherent with itself.</summary>
    DigestDiffers,
}
