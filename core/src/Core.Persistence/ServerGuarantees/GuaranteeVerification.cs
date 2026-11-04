using Core.Persistence.Custody;

namespace Core.Persistence.ServerGuarantees;

/// <summary>What one conference of the guarantees concluded: which ones are missing, and what the chain answered when the verdict was recorded as an event.</summary>
/// <param name="Missing">The guarantees that did not hold; <see cref="GuaranteeKinds.None" /> when every one did.</param>
/// <param name="Recorded">The answer of the chain: appended with its sequence, or the reason it was not.</param>
public sealed record GuaranteeVerification(GuaranteeKinds Missing, CustodyResult Recorded);
