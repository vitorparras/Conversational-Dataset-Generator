namespace Core.Host.Endpoints;

/// <summary>How the chain stands against the seal: whether it holds, which of the ways it does not, the words for it, and the moment and count the seal carried.</summary>
/// <param name="Holds">Whether the chain has the event sealed with the digest sealed.</param>
/// <param name="Outcome">The way the chain stands, by name.</param>
/// <param name="Statement">The standing in words.</param>
/// <param name="SealedAt">The moment of the seal.</param>
/// <param name="Events">The count of events the seal carried.</param>
internal sealed record SealStanding(bool Holds, string Outcome, string Statement, DateTimeOffset SealedAt, long Events);
