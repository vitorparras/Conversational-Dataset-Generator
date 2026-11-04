namespace Core.Persistence.ServerGuarantees.Restore;

/// <summary>What a load ended with and, when it did not restore, what it names: a version, a table, an event, two tips; never a value of any row.</summary>
public sealed record RestoreResult(RestoreOutcome Outcome, string Detail);
