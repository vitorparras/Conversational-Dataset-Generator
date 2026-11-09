namespace Core.Persistence.Custody;

/// <summary>The steps a custody event may record; the list lives here only, so that no free text reaches the chain.</summary>
public enum CustodyStep
{
    /// <summary>A person was added.</summary>
    PersonCreated,

    /// <summary>A person was deactivated.</summary>
    PersonDeactivated,

    /// <summary>A person was given another function.</summary>
    PersonFunctionChanged,

    /// <summary>The password of a person was set.</summary>
    PersonPasswordSet,

    /// <summary>An earlier event was corrected; the correction is an event of its own.</summary>
    Correction,

    /// <summary>The raw text of an item was read.</summary>
    RawTextRead,

    /// <summary>The sanitized text of an item was read.</summary>
    SanitizedTextRead,

    /// <summary>The map that reverses the pseudonyms of an item was read.</summary>
    ReversalEntriesRead,

    /// <summary>The chain was verified end to end; the reason says whether it held.</summary>
    VerifyChain,

    /// <summary>An item of the content was emptied, with the map that reverses its pseudonyms; the reason says why it was asked for.</summary>
    ItemEmptied,

    /// <summary>The emptying of an item was refused; the reason says why, and the item stays as it was.</summary>
    ItemEmptyingRefused,

    /// <summary>The keys of the content were wrapped again under a newer epoch; the item is that epoch.</summary>
    EpochRotated,

    /// <summary>A domain was created, with the keys of its two purposes named in the custody; the item is the domain.</summary>
    DomainCreated,

    /// <summary>The seal of the chain was recorded: the item is the chain, its id is the sequence of the event sealed, which is also the number of events the seal counts.</summary>
    SealRecorded,

    /// <summary>A domain passed to write under the next generation of its labels; the item is the domain and the reference is the generation.</summary>
    DomainLabelRotated,

    /// <summary>An attempt to sign in was accepted: the item is the attempt, the author is the person who signed in.</summary>
    SignInAccepted,

    /// <summary>An attempt to sign in was refused: the item is the attempt, and the reason says why, with the person as the author only when the password was wrong.</summary>
    SignInRefused,

    /// <summary>The credential of a person was derived again at sign-in, under the current work factor: the item and the author are that person.</summary>
    CredentialRecomputed,

    /// <summary>
    /// The loss of the master key, or of one label, was declared by whoever operates: the item is the key, its id is what was tried, and the reference is the scope.
    /// There is no way back.
    /// </summary>
    KeyLossDeclared,

    /// <summary>
    /// The recovery was rehearsed in a clean environment and its receipt was checked against the chain and the key of the production;
    /// the item is the key and its id the top of the copy.
    /// </summary>
    RecoveryTested,

    /// <summary>The guarantees of the server were conferred; the reason says whether every one held, and the item says which did not.</summary>
    VerifyGuarantees,

    /// <summary>A migration began with the credential that owns the tables; the reference says what for and the tip of the chain it found.</summary>
    OwnerWindowOpened,

    /// <summary>The window of the owner credential was closed by the application at its start; the reason says whether the chain and the guarantees held.</summary>
    OwnerWindowClosed,

    /// <summary>A copy of the data was put back in a new database; the reason says whether the chain and the guarantees held, and the reference says which copy and the tip before and after.</summary>
    Restored,

    /// <summary>The result of the rehearsal of a restore: the item is the chain, its id is the mask of what did not hold (zero when everything held) and the reference names it.</summary>
    RestoreRehearsed,

    /// <summary>A label of a domain was revoked, so that it encrypts nothing new and still decrypts; the item is the domain and the reference is the label.</summary>
    DomainLabelRevoked,

    /// <summary>A batch of the keys of a domain was wrapped again under the newest generation of its labels not revoked; the item is the domain.</summary>
    DomainRewrapped,

    /// <summary>A batch of ingestion was opened, with no content, by a person; the item is the batch.</summary>
    BatchOpened,

    /// <summary>A batch of ingestion was closed, accepted or rejected; the item is the batch, the reason says which, and a rejection carries its motive as the reference.</summary>
    BatchClosed,

    /// <summary>A person changed their own password: the item and the author are that person and the reference is how many of their other sessions it ended, never a value of a password.</summary>
    PasswordChanged,

    /// <summary>A person submitted a change of configuration on the screen; the item is the submission, which holds the key, the value and the motive, and the event has no reference.</summary>
    ConfigurationSubmitted,

    /// <summary>The origin that a submission declared for its batch was read; the item is the batch.</summary>
    OriginRead,

    /// <summary>The occurrences found in an item, with the destination of each, were read; the item is the one read from.</summary>
    OccurrencesRead,

    /// <summary>The sample of a batch was drawn by the system, once, at the request of the person who reviews it; the item is the batch.</summary>
    BatchSampled,
}
