namespace Core.Persistence.Custody;

/// <summary>One event as the table holds it, read for the verification: every field the digest covers, and the two digests the table holds.</summary>
internal sealed record StoredEvent(
    long Sequence,
    long OccurredAtMicroseconds,
    long? ActorPersonId,
    bool ActorSystem,
    string ItemKind,
    long ItemId,
    string Step,
    string Reason,
    long? Corrects,
    byte[] PreviousDigest,
    byte[] Digest,
    string RecordedBy);
