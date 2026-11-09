namespace Core.Persistence.Ingestion;

/// <summary>The motives of a rejection that the register itself understands, because it looks for something in the batches that carry them.</summary>
public static class BatchMotives
{
    /// <summary>The motive of the batch turned back because another batch of the domain had accepted the same file, recognized by the digest of what came and never by the name of the file.</summary>
    public const string AlreadyReceived = "already_received";
}
