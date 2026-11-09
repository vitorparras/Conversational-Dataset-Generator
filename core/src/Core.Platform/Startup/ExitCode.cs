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

    /// <summary>The revocation of a domain label was refused, nothing written: not a label, unknown, revoked or the last, a conflict with another writer, or the chain refused the event.</summary>
    DomainLabelRevocationRefused = 37,

    /// <summary>The database did not answer the revocation of a domain label, and nothing was written.</summary>
    DomainLabelRevocationFailed = 38,

    /// <summary>The rewrap of a domain stopped for a key it could not open or a label it was not given; the batches already written stay, and a new run goes on from them.</summary>
    DomainRewrapRefused = 39,

    /// <summary>The database did not answer the rewrap of a domain; the batches already written stay, and a new run goes on from them.</summary>
    DomainRewrapFailed = 40,

    /// <summary>The record of the rehearsal was refused and nothing was written because the chain already holds a record of that very receipt; the rehearsal is to be repeated.</summary>
    RecoveryReceiptRepeated = 41,

    /// <summary>The comparison of the server's audit log with the chain found a difference that is new.</summary>
    AuditDifferent = 20,

    /// <summary>The creation of the first person refused and wrote nothing: the registry already holds someone.</summary>
    PersonRegistryNotEmpty = 21,

    /// <summary>The creation of the first person refused and wrote nothing, for another reason: a field is not accepted, the credential is the owner one, or the chain refused.</summary>
    PersonCreationRefused = 22,

    /// <summary>The creation of the first person could not talk to the database, or stopped between the person and the password.</summary>
    PersonCreationFailed = 23,

    /// <summary>The making of the copy was refused and nothing was written: the folder holds something, the chain did not take the seal, or another write reached it first.</summary>
    CopyRefused = 31,

    /// <summary>The making of the copy of the data could not talk to the database or write the folder; what was written is to be removed and the copy repeated.</summary>
    CopyFailed = 32,

    /// <summary>The rehearsal of the restore concluded that something did not hold, or could not record its result: the restored database is not to be trusted.</summary>
    RehearsalFailed = 33,

    /// <summary>The rehearsal of the restore was refused before it began: the seal noted outside the copy was not given once or is not a seal this version reads.</summary>
    RehearsalRefused = 34,

    /// <summary>The database did not answer the rehearsal of the restore, and nothing was concluded.</summary>
    RehearsalNotRun = 35,

    /// <summary>The load of a copy in a new database did not restore it: the copy, the database or a check of the result refused.</summary>
    RestoreRefused = 36,

    /// <summary>The applier of the configuration was refused and nothing was written: its file, its commands or its credential of the database did not hold.</summary>
    ApplierRefused = 37,

    /// <summary>The applier could not talk to the database, or applied and the restart of the core did not happen.</summary>
    ApplierFailed = 38,
}
