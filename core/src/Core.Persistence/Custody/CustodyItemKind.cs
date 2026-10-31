namespace Core.Persistence.Custody;

/// <summary>The kinds of item a custody event may be about; the list lives here only, so that no free text reaches the chain.</summary>
public enum CustodyItemKind
{
    /// <summary>A person of the registry.</summary>
    Person,
}
