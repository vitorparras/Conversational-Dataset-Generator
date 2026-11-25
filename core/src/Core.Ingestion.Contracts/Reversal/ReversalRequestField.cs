namespace Core.Ingestion.Contracts.Reversal;

/// <summary>A field of the request to reverse a marker, named when it is missing or is not what it must be.</summary>
public enum ReversalRequestField
{
    /// <summary>The item, a positive number.</summary>
    Item = 0,

    /// <summary>The marker, the prefix of its kind and eight digits.</summary>
    Marker = 1,

    /// <summary>The term, a positive number of days.</summary>
    Term = 2,

    /// <summary>Why the identity is needed.</summary>
    Reason = 3,

    /// <summary>What will be done with it.</summary>
    Use = 4,
}
