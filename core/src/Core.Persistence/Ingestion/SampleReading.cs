namespace Core.Persistence.Ingestion;

/// <summary>The reading of the text of an item of the sample, for whoever reviews it: the chain records who read which item before the text is opened.</summary>
public sealed record SampleReading(int DomainId, long BatchId, long ItemId, long ReaderId);
