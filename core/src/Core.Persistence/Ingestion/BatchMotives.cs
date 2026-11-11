namespace Core.Persistence.Ingestion;

/// <summary>The motives of a rejection that the register itself understands, because it looks for something in the batches that carry them.</summary>
public static class BatchMotives
{
    /// <summary>The same motive as a constant, for the queries that write it into their text.</summary>
    internal const string AlreadyReceivedMotive = "already_received";

    /// <summary>The motive of the batch turned back because another batch of the domain had accepted the same file, recognized by the digest of what came and never by the name of the file.</summary>
    public static string AlreadyReceived => AlreadyReceivedMotive;
}
