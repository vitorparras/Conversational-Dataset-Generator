namespace Core.Persistence.Ingestion;

/// <summary>A batch as the database holds it, whatever its state: a rejected batch stays consultable with its motive after its material is gone.</summary>
/// <param name="State">Where the batch is.</param>
/// <param name="Motive">Why the batch was rejected; null unless it was.</param>
/// <param name="OpenedBy">The person who opened it.</param>
public sealed record IngestionBatch(BatchState State, string? Motive, long OpenedBy);
