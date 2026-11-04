using Core.Persistence.Custody.Verification;

namespace Core.Persistence.ServerGuarantees;

/// <summary>What the making of a copy of the data concluded, with the seal that is to be noted outside the copy.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="Seal">The seal of the chain recorded just before the rows were read; null unless the copy was made. It is not in the folder: noting it apart is what makes it proof.</param>
public sealed record DataCopyResult(DataCopyOutcome Outcome, ChainSeal? Seal);
