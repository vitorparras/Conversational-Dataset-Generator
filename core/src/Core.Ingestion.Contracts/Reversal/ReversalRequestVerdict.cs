namespace Core.Ingestion.Contracts.Reversal;

/// <summary>What the judgment of a request to reverse a marker comes to.</summary>
public enum ReversalRequestVerdict
{
    /// <summary>One marker in one item, with the reason, the use and the term: it may be asked for.</summary>
    Accepted = 0,

    /// <summary>A field is missing or is not what it must be: nothing is asked for and nothing is recorded.</summary>
    Incomplete = 1,

    /// <summary>It reaches more than one marker or item, or no item: refused by its scope, and the refusal is recorded.</summary>
    TooWide = 2,
}
