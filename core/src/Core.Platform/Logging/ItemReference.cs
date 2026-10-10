namespace Core.Platform.Logging;

/// <summary>
/// What a log line carries in place of content: enough to reach the item and the step, never what
/// the item says (requirement 021).
/// </summary>
/// <param name="ItemId">The opaque identifier of the item.</param>
/// <param name="Step">The step the item was in.</param>
/// <param name="Reason">Why it failed, in words that carry no content.</param>
public readonly record struct ItemReference(string ItemId, string Step, string Reason);
