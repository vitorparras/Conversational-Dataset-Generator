namespace Core.Persistence.Recovery;

/// <summary>How the record of a rehearsal ended; every value but <see cref="Recorded" /> is a refusal that wrote nothing.</summary>
public enum RecoveryOutcome
{
    /// <summary>The step was written to the chain.</summary>
    Recorded,

    /// <summary>The text is not a receipt that holds: it was altered, cut short or is of another form.</summary>
    ReceiptRefused,

    /// <summary>The receipt holds, but the top of the copy is not in this chain, or the key is not the one of the custody here, or both; the failures say which.</summary>
    NotConfirmed,

    /// <summary>The chain refused the event, or another writer reached the same link first.</summary>
    ChainRefused,
}
