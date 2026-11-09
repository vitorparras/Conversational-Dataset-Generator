namespace Core.Persistence.Ingestion;

/// <summary>What the database holds of the sample of a batch.</summary>
/// <param name="State">Where the batch is.</param>
/// <param name="Sample">The numbers of the items drawn; null while the sample was not drawn, empty for a batch with no item.</param>
/// <param name="SampledAt">When the sample was drawn, by the clock of the server; null while it was not drawn.</param>
public sealed record BatchReview(BatchState State, IReadOnlyList<long>? Sample, DateTimeOffset? SampledAt);
