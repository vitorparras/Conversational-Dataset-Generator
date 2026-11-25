namespace Core.Persistence.Ingestion;

/// <summary>What became of a pseudonymized item: written, with its identifier, or the batch rejected because an entry of its map was not of an occurrence to pseudonymize.</summary>
/// <param name="ItemId">The item written; null when the batch was rejected.</param>
/// <param name="Rejection">The rejection of the batch, with the event of the chain; null when the item was written.</param>
public sealed record PseudonymizedItem(long? ItemId, BatchResult? Rejection);
