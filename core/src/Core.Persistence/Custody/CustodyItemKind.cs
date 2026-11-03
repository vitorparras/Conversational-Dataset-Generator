namespace Core.Persistence.Custody;

/// <summary>The kinds of item a custody event may be about; the list lives here only, so that no free text reaches the chain.</summary>
public enum CustodyItemKind
{
    /// <summary>A person of the registry.</summary>
    Person,

    /// <summary>An item of the content, which holds the raw and the sanitized text.</summary>
    ItemText,

    /// <summary>The map that reverses the pseudonyms of an item.</summary>
    ReversalMap,

    /// <summary>The chain itself: what a verification of the chain is about.</summary>
    CustodyChain,

    /// <summary>An epoch of the content keys, by its number: what a rotation of the epoch is about.</summary>
    ContentEpoch,

    /// <summary>A domain of the installation, by its identifier: what the creation of a domain is about.</summary>
    Domain,

    /// <summary>The master key of the installation: what the declaration of its loss is about; the id is the set of what was tried to recover it.</summary>
    MasterKey,
}
