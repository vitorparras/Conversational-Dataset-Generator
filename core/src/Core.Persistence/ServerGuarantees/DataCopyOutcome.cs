namespace Core.Persistence.ServerGuarantees;

/// <summary>How the making of a copy of the data ended.</summary>
public enum DataCopyOutcome
{
    /// <summary>The copy was written and the seal of the chain at that moment was recorded.</summary>
    Made,

    /// <summary>The folder holds something already, and nothing was written: a copy is never mixed with what is there.</summary>
    FolderNotEmpty,

    /// <summary>The chain did not take the event of the seal, which happens while the last verification failed or when another writer was first; nothing was written.</summary>
    SealNotRecorded,

    /// <summary>Another write reached the chain between the record of the seal and the snapshot, so the top of the copy would not be that record; nothing was written in the folder.</summary>
    ChainMoved,
}
