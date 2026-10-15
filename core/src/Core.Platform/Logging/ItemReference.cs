namespace Core.Platform.Logging;

/// <summary>What a log line may carry about an item in place of its content: which item, in which step, and why.</summary>
/// <param name="ItemId">The identifier of the item, which identifies no person by itself.</param>
/// <param name="Step">The step the item was in.</param>
/// <param name="Reason">Why the step did not complete, in words that quote nothing from the item.</param>
public sealed record ItemReference(string ItemId, string Step, string Reason);
