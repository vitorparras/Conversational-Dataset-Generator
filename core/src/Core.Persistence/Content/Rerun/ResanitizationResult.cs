namespace Core.Persistence.Content.Rerun;

/// <summary>The answer of the rerun of a held item.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="Refusal">Why it was refused, only when <see cref="RerunOutcome.Refused"/>.</param>
public sealed record ResanitizationResult(RerunOutcome Outcome, RerunRefusal? Refusal = null);
