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

    /// <summary>The guarantees of the server were conferred; the reason says whether every one held, and the item says which did not.</summary>
    VerifyGuarantees,

    /// <summary>A migration began with the credential that owns the tables; the reference says what for and the tip of the chain it found.</summary>
    OwnerWindowOpened,

    /// <summary>The window of the owner credential was closed by the application at its start; the reason says whether the chain and the guarantees held.</summary>
    OwnerWindowClosed,

    /// <summary>The keys of the content were wrapped again under a newer epoch; the item is that epoch.</summary>
    EpochRotated,

    /// <summary>A domain was created, with the keys of its two purposes named in the custody; the item is the domain.</summary>
    DomainCreated,

    /// <summary>The seal of the chain was recorded: the item is the chain, its id is the sequence of the event sealed, which is also the number of events the seal counts.</summary>
    SealRecorded,

    /// <summary>A domain passed to write under the next generation of its labels; the item is the domain and the reference is the generation.</summary>
    DomainLabelRotated,

    /// <summary>
    /// The loss of the master key, or of one label, was declared by whoever operates: the item is the key, its id is what was tried, and the reference is the scope.
    /// There is no way back.
    /// </summary>
    KeyLossDeclared,

    /// <summary>The result of the rehearsal of a restore: the item is the chain, its id is the mask of what did not hold (zero when everything held) and the reference names it.</summary>
    RestoreRehearsed,
}
