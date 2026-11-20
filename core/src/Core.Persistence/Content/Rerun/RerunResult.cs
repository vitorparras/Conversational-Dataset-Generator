namespace Core.Persistence.Content.Rerun;

/// <summary>How a request or the rerun of an item ended, the request written or open, and why an item was refused.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="RerunId">The request written or the one open; none otherwise.</param>
/// <param name="Refusal">Why the item was refused, only when <see cref="RerunOutcome.Refused"/>.</param>
public sealed record RerunResult(RerunOutcome Outcome, long? RerunId = null, RerunRefusal? Refusal = null);
