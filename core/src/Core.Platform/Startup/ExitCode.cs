namespace Core.Platform.Startup;

/// <summary>What the process reports to whoever started it when it ends.</summary>
public enum ExitCode
{
    /// <summary>The process ran and ended as asked.</summary>
    Success = 0,

    /// <summary>A startup check failed, so the process did not open its port; 69 is the unavailable service of the sysexits convention.</summary>
    StartupCheckFailed = 69,

    /// <summary>A failure ended the process once the host was assembled, when it opened its port or served, such as a port already taken; 70 is the software error of sysexits.</summary>
    ServingFailed = 70,

    /// <summary>The configuration was refused before anything else started; 78 is the configuration error of the sysexits convention.</summary>
    ConfigurationRefused = 78,

    /// <summary>The migration found the schema already at the last step and ran nothing.</summary>
    MigrationNothingToDo = 10,

    /// <summary>The migration refused: the registry holds a step this build does not carry.</summary>
    MigrationRefusedAhead = 11,

    /// <summary>The migration refused: a step already applied no longer has the content that was registered.</summary>
    MigrationRefusedDivergent = 12,

    /// <summary>A step of the migration failed and was undone; the steps before it stay applied.</summary>
    MigrationStepFailed = 13,

    /// <summary>The migration could not reach the server or read the registry, and ran nothing.</summary>
    MigrationNotConnected = 14,

    /// <summary>The rotation of the epoch refused and changed nothing: a file of the custody was not given or read, or a key could not be unwrapped.</summary>
    EpochRotationRefused = 15,

    /// <summary>The rotation of the epoch could not talk to the database and changed nothing.</summary>
    EpochRotationFailed = 16,

    /// <summary>The creation of a domain refused and wrote nothing: the derivation label of a purpose is missing from the custody file.</summary>
    DomainLabelsMissing = 17,

    /// <summary>The creation of a domain refused and wrote nothing: a domain of that name exists already.</summary>
    DomainNameTaken = 18,

    /// <summary>The creation of a domain refused and wrote nothing, for another reason: a custody file was not read, the name is not valid, the chain refused or another writer was first.</summary>
    DomainCreationRefused = 19,

    /// <summary>The creation of a domain could not talk to the database and wrote nothing.</summary>
    DomainCreationFailed = 26,

    /// <summary>The rotation of a domain label was refused and nothing was written: the domain is not accepted, or the label file could not be read.</summary>
    DomainLabelRefused = 24,

    /// <summary>The database did not answer the rotation of a domain label, and nothing was written.</summary>
    DomainLabelFailed = 25,

    /// <summary>The declaration of the loss of a key was refused and nothing was written: the scope or what was tried is not one of the accepted, or the chain refused the event.</summary>
    KeyLossRefused = 27,

    /// <summary>The database did not answer the declaration of the loss of a key, and nothing was written.</summary>
    KeyLossFailed = 28,

    /// <summary>
    /// The rehearsal of the recovery, or its record, was refused and nothing was written: a sample did not open,
    /// the receipt did not hold or the chain or the key did not confirm it.
    /// </summary>
    RecoveryRefused = 29,

    /// <summary>The database did not answer the rehearsal of the recovery or its record.</summary>
    RecoveryFailed = 30,

    /// <summary>The comparison of the server's audit log with the chain found a difference that is new.</summary>
    AuditDifferent = 20,
}
