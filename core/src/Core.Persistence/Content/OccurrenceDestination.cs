namespace Core.Persistence.Content;

/// <summary>What was decided for an occurrence of an item when it came in: these three, and no other. The decision is not made again by any later stage.</summary>
public enum OccurrenceDestination
{
    /// <summary>The span goes out of the text.</summary>
    Remove = 0,

    /// <summary>The span becomes a stable marker.</summary>
    Pseudonymize = 1,

    /// <summary>The span stays as it is.</summary>
    Preserve = 2,
}
