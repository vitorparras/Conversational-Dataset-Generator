namespace Core.Persistence.Ingestion;

/// <summary>What a submission brings to the batch it opens: its receipt and where it says the material came from, always together, because the type has no way to hold one without the other.</summary>
/// <param name="Receipt">What the submission brought, kept with the batch.</param>
/// <param name="Origin">Where the submission says the material came from, sealed or declared unknown.</param>
public sealed record SubmissionOpening(SubmissionReceipt Receipt, OriginDeclaration Origin);
