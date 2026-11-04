using Core.Persistence.Custody;

namespace Core.Persistence.ServerGuarantees;

/// <summary>What one conference of the guarantees concluded: which ones are missing, and what the chain answered when the verdict was recorded as an event.</summary>
/// <param name="Missing">The guarantees that did not hold; <see cref="GuaranteeKinds.None" /> when every one did.</param>
/// <param name="Recorded">The answer of the chain: appended with its sequence, or the reason it was not.</param>
/// <param name="NoSampleOutsideLoss">
/// Information, not a guarantee missing: everything the sample of sealed records could reach is under the scope of a declared loss of the key, so no record was opened.
/// It is not in the mask nor in the event of the chain.
/// </param>
public sealed record GuaranteeVerification(GuaranteeKinds Missing, CustodyResult Recorded, bool NoSampleOutsideLoss = false);
