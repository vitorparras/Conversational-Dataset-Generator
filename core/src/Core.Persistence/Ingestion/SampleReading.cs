namespace Core.Persistence.Ingestion;

/// <summary>The reading of the text of an item of the sample, for whoever reviews it: the chain records who read which item before the text is opened.</summary>
/// <param name="DomainId">The domain of the batch.</param>
/// <param name="BatchId">The batch whose sample is being reviewed.</param>
/// <param name="ItemId">The item of the sample to read.</param>
/// <param name="ReaderId">The person who reads.</param>
public sealed record SampleReading(int DomainId, long BatchId, long ItemId, long ReaderId);
