namespace Core.Persistence.Content.Rerun;

/// <summary>An item a request refused: the item, its batch and why.</summary>
/// <param name="ItemId">The item.</param>
/// <param name="BatchId">Its batch.</param>
/// <param name="Refusal">Why.</param>
public sealed record RerunItemRefusal(long ItemId, long BatchId, RerunRefusal Refusal);
