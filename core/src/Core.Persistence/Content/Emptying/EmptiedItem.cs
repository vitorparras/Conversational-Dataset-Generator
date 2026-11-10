namespace Core.Persistence.Content.Emptying;

/// <summary>What the system keeps of an item that was emptied: when, by whom, why and the protocol of whoever asked. Never a text of the item, and never the name of whoever asked.</summary>
/// <param name="At">When the emptying was recorded in the chain.</param>
/// <param name="Executor">The name of the person who emptied, as the registry of people holds it.</param>
/// <param name="Motive">Why the emptying was asked for.</param>
/// <param name="Reference">The number of the protocol outside the system that asked.</param>
public sealed record EmptiedItem(DateTimeOffset At, string Executor, EmptyingMotive Motive, string Reference);
