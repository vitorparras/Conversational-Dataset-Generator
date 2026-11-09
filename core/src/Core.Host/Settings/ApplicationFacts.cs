namespace Core.Host.Settings;

/// <summary>What the chain and the registry say of an approved change when the applier is about to apply it: whether each author is still active with the function, and whether the chain closes.</summary>
/// <param name="SubmitterAllowed">Whether the person who submitted is active and has the technical administration.</param>
/// <param name="ApproverAllowed">Whether the person who approved is active and has the technical administration.</param>
/// <param name="ChainClosed">Whether the verification of the chain found every link as it should be.</param>
internal sealed record ApplicationFacts(bool SubmitterAllowed, bool ApproverAllowed, bool ChainClosed);
