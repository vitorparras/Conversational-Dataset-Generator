namespace Core.Ingestion.Contracts.Reversal;

/// <summary>How far a request reaches when it is not one marker in one item; the refusal names it.</summary>
public enum ReversalScope
{
    /// <summary>No item declared: the marker wherever it is, in the whole domain.</summary>
    Domain = 0,

    /// <summary>More than one item.</summary>
    ManyItems = 1,

    /// <summary>More than one marker.</summary>
    ManyMarkers = 2,
}
