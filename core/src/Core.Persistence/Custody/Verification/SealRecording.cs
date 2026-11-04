namespace Core.Persistence.Custody.Verification;

/// <summary>The answer of recording the seal: the seal made, and what the chain answered when the event of the record was added.</summary>
public sealed record SealRecording
{
    /// <summary>What the chain answered: appended with its sequence, or the reason it was not.</summary>
    public required CustodyResult Recorded { get; init; }

    /// <summary>The seal the record is of; null when the event was not added, because then nothing was sealed.</summary>
    public ChainSeal? Seal { get; init; }
}
