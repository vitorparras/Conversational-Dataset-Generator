using Core.Persistence.Custody.Verification;

namespace Core.Persistence.ServerGuarantees;

/// <summary>What the making of a copy of the data concluded, with the seal that is to be noted outside the copy.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="Seal">The top of the copy: sequence and digest of the event of the seal, which the snapshot ends at; null unless made. Not in the folder.</param>
public sealed record DataCopyResult(DataCopyOutcome Outcome, ChainSeal? Seal);
