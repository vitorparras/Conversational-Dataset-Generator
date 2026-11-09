namespace Core.Persistence.Custody;

/// <summary>The reasons a custody event may give; the list lives here only, so that no free text reaches the chain.</summary>
public enum CustodyReason
{
    /// <summary>An act of whoever administers, done on purpose.</summary>
    AdministrativeAct,

    /// <summary>The correction of what an earlier event recorded.</summary>
    CorrectionOfRecord,

    /// <summary>Protected content was opened by whoever the event names.</summary>
    ContentRead,

    /// <summary>The verification of the chain found every link whole.</summary>
    VerificationPassed,

    /// <summary>The verification of the chain found an event that does not hold.</summary>
    VerificationFailed,

    /// <summary>The emptying was asked for by the person the content is about.</summary>
    RemovalRequestedBySubject,

    /// <summary>The emptying was required by the contract with whoever handed the content over.</summary>
    ContractRequirement,

    /// <summary>The emptying was refused because its author does not hold the function that empties.</summary>
    AuthorWithoutFunction,

    /// <summary>The emptying was refused because the item had been emptied already.</summary>
    ItemAlreadyEmptied,

    /// <summary>The attempt to sign in was accepted.</summary>
    CredentialsAccepted,

    /// <summary>The attempt to sign in was refused because the password of the person was wrong.</summary>
    PasswordWrong,

    /// <summary>The attempt to sign in was refused for a name nobody goes by or an account that does not enter; the name typed is not kept.</summary>
    AccountNotRecognized,

    /// <summary>The batch of ingestion was accepted: what it holds is now part of the stock.</summary>
    BatchAccepted,

    /// <summary>The batch of ingestion was rejected: what it held was removed, and the motive is in the reference of the event.</summary>
    BatchRejected,

    /// <summary>The review of the sample of a batch found nothing.</summary>
    ReviewClean,

    /// <summary>The review of the sample of a batch found personal data not treated, and the whole batch was rejected.</summary>
    ReviewFound,
}
