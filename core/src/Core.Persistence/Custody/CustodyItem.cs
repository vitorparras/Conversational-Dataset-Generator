namespace Core.Persistence.Custody;

/// <summary>What an event is about: the kind and the numeric identifier, and nothing of its content.</summary>
/// <param name="Kind">One of the kinds the chain knows.</param>
/// <param name="Id">The identifier of the item within its kind.</param>
public sealed record CustodyItem(CustodyItemKind Kind, long Id);
