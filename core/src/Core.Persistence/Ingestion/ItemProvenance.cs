namespace Core.Persistence.Ingestion;

/// <summary>Where an item came from: the batch it was kept in, when the submission of that batch came and who submitted it. Nothing in it is a word of the material or the name of the file.</summary>
/// <param name="BatchId">The batch of the entrance.</param>
/// <param name="SubmittedAt">When the submission came, by the clock of the server.</param>
/// <param name="SubmittedBy">The person who submitted.</param>
/// <param name="Submission">The identifier the system generated for the submission; null for a batch no submission opened.</param>
public sealed record ItemProvenance(long BatchId, DateTimeOffset SubmittedAt, long SubmittedBy, string? Submission);
