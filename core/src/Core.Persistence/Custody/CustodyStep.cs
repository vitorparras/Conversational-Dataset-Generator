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
}
