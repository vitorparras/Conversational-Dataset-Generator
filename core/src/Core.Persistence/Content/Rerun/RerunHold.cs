namespace Core.Persistence.Content.Rerun;

/// <summary>An item of the queue a worker holds: the entry, its request, the item, the version the request reruns to, the moment the hold expires and the tries spent before it.</summary>
/// <param name="DomainId">The domain of the item.</param>
/// <param name="Entry">The entry of the item in the queue.</param>
/// <param name="RerunId">The request the entry belongs to.</param>
/// <param name="ItemId">The item.</param>
/// <param name="Version">The version the request reruns to.</param>
/// <param name="Until">When the hold expires, as the database keeps it; it also tells this hold from a later one of the same entry.</param>
/// <param name="Tries">The tries spent before this hold.</param>
public sealed record RerunHold(int DomainId, long Entry, long RerunId, long ItemId, string Version, DateTimeOffset Until, int Tries);
