namespace Core.Persistence.ServerGuarantees.Restore;

/// <summary>What a load ended with and, when it did not restore, what it names: a version, a table, an event, two tips; never a value of any row.</summary>
/// <param name="Outcome">How the load ended.</param>
/// <param name="Detail">The divergence, empty when the copy was restored.</param>
public sealed record RestoreResult(RestoreOutcome Outcome, string Detail);
