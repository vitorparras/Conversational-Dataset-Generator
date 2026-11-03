using Core.Persistence.ServerGuarantees;

namespace Core.Host;

/// <summary>What the comparison mode found: the differences between the server's log and the chain, or the kind of failure that kept it from comparing.</summary>
/// <param name="Differences">The differences found, the known ones among them; empty when the layers agree or the comparison failed.</param>
/// <param name="Failure">The type of the exception that stopped the comparison, or null when it ran.</param>
internal sealed record AuditCompareOutcome(IReadOnlyList<AuditDifference> Differences, string? Failure);
