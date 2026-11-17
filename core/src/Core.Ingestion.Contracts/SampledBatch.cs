namespace Core.Ingestion.Contracts;

/// <summary>What the review of a sample asks of a batch that exists, read where the batch is held and handed over without the row it came from.</summary>
/// <param name="Open">Whether the batch still waits for its acceptance or its rejection.</param>
/// <param name="Sampled">Whether the sample of the batch was already drawn.</param>
/// <param name="Reviewed">Whether the sample of the batch was already reviewed by someone.</param>
public readonly record struct SampledBatch(bool Open, bool Sampled, bool Reviewed);
