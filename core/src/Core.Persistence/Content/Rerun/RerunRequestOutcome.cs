namespace Core.Persistence.Content.Rerun;

/// <summary>How a request of a rerun ended.</summary>
public enum RerunRequestOutcome
{
    /// <summary>The request was written and its items queued.</summary>
    Queued,

    /// <summary>No item was left to queue and a request to the version is open: nothing was written, and the open one is answered.</summary>
    AlreadyOpen,

    /// <summary>No item was left to queue and no request is open: nothing was written.</summary>
    NothingToRerun,

    /// <summary>The chain did not record the request, and nothing was kept.</summary>
    NotRecorded,
}
