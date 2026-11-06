namespace Core.Persistence.Ingestion;

/// <summary>Where a batch of ingestion is: it takes content while open, and closes once, accepted or rejected.</summary>
public enum BatchState
{
    /// <summary>The batch takes content, and none of it can be read as stock yet.</summary>
    Open,

    /// <summary>The batch was accepted: what it holds is stock.</summary>
    Accepted,

    /// <summary>The batch was rejected: what it held was removed, and the motive says why.</summary>
    Rejected,
}
