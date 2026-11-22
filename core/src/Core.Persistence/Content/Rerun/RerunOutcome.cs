namespace Core.Persistence.Content.Rerun;

/// <summary>How a request of a rerun, or the rerun of a held item, ended.</summary>
public enum RerunOutcome
{
    /// <summary>The request was written and its items queued.</summary>
    Queued,

    /// <summary>No item was left to queue and a request is open: nothing was written, and the open one is answered.</summary>
    AlreadyOpen,

    /// <summary>No item was left to queue and no request is open: nothing was written.</summary>
    NothingToRerun,

    /// <summary>The text produced is the one the item holds; it was not written again.</summary>
    Identical,

    /// <summary>The sanitized text of the item was replaced.</summary>
    Replaced,

    /// <summary>The item was concluded as refused, with the reason.</summary>
    Refused,

    /// <summary>The item went back to the queue without spending a try.</summary>
    Returned,

    /// <summary>The item spent a try and went back to the queue.</summary>
    Failed,

    /// <summary>The hold had expired and the item was no longer held by this worker; nothing was written.</summary>
    NotHeld,

    /// <summary>The chain did not record the event, and nothing was kept.</summary>
    NotRecorded,
}
