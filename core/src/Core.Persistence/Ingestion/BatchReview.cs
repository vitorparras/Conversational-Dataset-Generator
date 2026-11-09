namespace Core.Persistence.Ingestion;

/// <summary>What the database holds of the sample of a batch: its state, the numbers of the items drawn (null while not drawn) and the server's moment of the draw.</summary>
public sealed record BatchReview(BatchState State, IReadOnlyList<long>? Sample, DateTimeOffset? SampledAt);
