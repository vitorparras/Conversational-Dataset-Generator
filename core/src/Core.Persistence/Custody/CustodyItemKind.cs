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
}
