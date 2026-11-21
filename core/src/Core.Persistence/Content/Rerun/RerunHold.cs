namespace Core.Persistence.Content.Rerun;

/// <summary>An item a worker holds until a moment, which the database keeps and which tells this hold from a later one, with the tries spent before it.</summary>
/// <param name="DomainId">The domain.</param>
/// <param name="Entry">The entry in the queue.</param>
/// <param name="RerunId">The request.</param>
/// <param name="ItemId">The item.</param>
/// <param name="Version">The version the request reruns to.</param>
/// <param name="Until">When the hold expires.</param>
/// <param name="Tries">The tries spent.</param>
public readonly record struct RerunHold(int DomainId, long Entry, long RerunId, long ItemId, string Version, DateTimeOffset Until, int Tries);
