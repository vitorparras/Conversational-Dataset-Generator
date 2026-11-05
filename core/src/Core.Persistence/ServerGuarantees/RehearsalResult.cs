using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;

namespace Core.Persistence.ServerGuarantees;

/// <summary>What one rehearsal of a restore concluded: what did not hold, the chain against the seal, and what the chain answered when the result was recorded as an event.</summary>
/// <param name="Findings">What did not hold; <see cref="RehearsalFindings.None" /> when everything did.</param>
/// <param name="Verification">The verification of the links, with the seal compared, which the rehearsal made and recorded.</param>
/// <param name="Recorded">The answer of the chain to the event of the result: appended with its sequence, or the reason it was not, which is the case when the links do not hold.</param>
public sealed record RehearsalResult(RehearsalFindings Findings, ChainVerification Verification, CustodyResult Recorded)
{
    /// <summary>Whether everything the rehearsal conferred held.</summary>
    public bool Passed => Findings == RehearsalFindings.None;

    /// <summary>The names of what did not hold, apart by a dot; <c>held</c> when nothing failed.</summary>
    public string Names => RestoreRehearsal.NamesOf(Findings);
}
