namespace Core.Persistence.Ingestion;

/// <summary>How an attempt to open or to close a batch ended; every value but the first three wrote nothing.</summary>
public enum BatchOutcome
{
    /// <summary>The batch was opened, with the event of its opening in the chain.</summary>
    Opened,

    /// <summary>The batch was accepted, with the event of its closing in the chain.</summary>
    Accepted,

    /// <summary>The batch was rejected, its content removed, with the event of its closing in the chain.</summary>
    Rejected,

    /// <summary>The chain refused the event, so nothing was written; <see cref="BatchResult.Refusal" /> says why.</summary>
    NotRecorded,

    /// <summary>The domain holds no such batch.</summary>
    NotFound,

    /// <summary>The batch was closed already: it closes once.</summary>
    AlreadyClosed,

    /// <summary>The motive is not a name of lowercase letters, digits and underscores, so the batch was left as it was.</summary>
    MotiveRefused,

    /// <summary>The person named to open the batch is not registered.</summary>
    AuthorUnknown,

    /// <summary>The domain named to open the batch is not registered.</summary>
    DomainUnknown,

    /// <summary>The condition declared to open the batch is not one the installation knows, so nothing was written.</summary>
    ConditionUnknown,

    /// <summary>Another batch of the domain accepted the same file first, so this one was left open, with its items, for whoever closes it to reject.</summary>
    AlreadyReceived,
}
