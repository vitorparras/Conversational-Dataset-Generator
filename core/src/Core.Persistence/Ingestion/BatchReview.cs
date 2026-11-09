namespace Core.Persistence.Ingestion;

/// <summary>The review of a batch as it stands, for whoever asks which sample was reviewed, by whom and when: nothing is chosen here, only read.</summary>
/// <param name="State">Where the batch is.</param>
/// <param name="Sample">The numbers of the items drawn; null while the sample was not drawn.</param>
/// <param name="SampledAt">When the sample was drawn, the server's moment.</param>
/// <param name="ReviewerId">Who reviewed the sample; null while it was not reviewed.</param>
/// <param name="ReviewedAt">When the sample was reviewed, the server's moment.</param>
/// <param name="Finding">What the review found, which rejected the batch; null when it found nothing or was not done.</param>
public sealed record BatchReview(
    BatchState State, IReadOnlyList<long>? Sample, DateTimeOffset? SampledAt, long? ReviewerId, DateTimeOffset? ReviewedAt, string? Finding);
