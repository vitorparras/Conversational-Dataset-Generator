namespace Core.Persistence.Ingestion;

/// <summary>The request to draw the sample of a batch for review: whoever asks does not choose the items, nor how many.</summary>
public sealed record BatchSampling(int DomainId, long BatchId, long ReviewerId);
