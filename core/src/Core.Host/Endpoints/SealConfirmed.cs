namespace Core.Host.Endpoints;

/// <summary>The record of a seal that whoever operates kept outside the base: the sequence the record took in the chain, and the moment and the count the seal carried.</summary>
/// <param name="Sequence">The sequence of the event of the record.</param>
/// <param name="Events">The count of events the seal carried, which is the sequence of the event sealed.</param>
/// <param name="SealedAt">The moment of the seal.</param>
internal sealed record SealConfirmed(long Sequence, long Events, DateTimeOffset SealedAt);
