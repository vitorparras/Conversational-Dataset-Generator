namespace Core.Persistence.ServerGuarantees.Restore;

/// <summary>How a load of a copy ended.</summary>
public enum RestoreOutcome
{
    /// <summary>The copy is back, the chain holds, the guarantees hold and the chain records the restore.</summary>
    Restored,
    /// <summary>The copy was refused before anything was put back, or while it was read: its version is not the database's, or it cannot be read.</summary>
    CopyRefused,
    /// <summary>The window of the load did not open: the database has held something, or the window was used already.</summary>
    NotEmpty,
    /// <summary>The database refused a table or an event, which the result names; the window closed as failed.</summary>
    RowsRefused,
    /// <summary>The last event put back is not the tip noted outside the copy; the window closed as failed.</summary>
    TipDiffers,
    /// <summary>The chain put back did not pass its verification, or did not take the event of the restore.</summary>
    ChainBroken,
    /// <summary>The chain holds and a guarantee of the server is missing; the restore is recorded as failed.</summary>
    GuaranteesMissing,
}
