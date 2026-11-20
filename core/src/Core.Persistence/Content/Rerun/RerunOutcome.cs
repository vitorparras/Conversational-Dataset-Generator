namespace Core.Persistence.Content.Rerun;

/// <summary>How the rerun of a held item ended.</summary>
public enum RerunOutcome
{
    /// <summary>The text produced is the one the item holds; it was not written again.</summary>
    Identical,

    /// <summary>The sanitized text of the item was replaced.</summary>
    Replaced,

    /// <summary>The item was concluded as refused; the refusal says why.</summary>
    Refused,

    /// <summary>The database stopped the transaction for another; the item went back to the queue without spending a try.</summary>
    Returned,

    /// <summary>The try was spent and the item went back to the queue.</summary>
    Failed,

    /// <summary>The hold had expired and the item was no longer held by this worker; nothing was written.</summary>
    NotHeld,

    /// <summary>The chain did not record the conclusion, and nothing was kept.</summary>
    NotRecorded,
}
