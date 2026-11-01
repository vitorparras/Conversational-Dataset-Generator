namespace Core.Persistence.Custody;

/// <summary>The way an event of the chain does not hold; the three ways are checked in this order, and the first that fails is the one told.</summary>
public enum ChainInconsistency
{
    /// <summary>The sequence is not the one after the event before: an event between them is missing, or the chain does not start at one.</summary>
    SequenceGap,

    /// <summary>The event does not point to the digest of the event before it.</summary>
    PreviousDigestMismatch,

    /// <summary>The digest the event holds is not the digest of the event as it stands.</summary>
    DigestMismatch,
}
