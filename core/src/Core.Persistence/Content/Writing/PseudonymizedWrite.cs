namespace Core.Persistence.Content.Writing;

/// <summary>What became of a pseudonymized item: written, with its identifier, or nothing written because some of its markers were held already, which are named.</summary>
/// <param name="ItemId">The item written; null when nothing was.</param>
/// <param name="Taken">The markers held already in the installation, to be drawn again; empty when the item was written.</param>
public sealed record PseudonymizedWrite(long? ItemId, IReadOnlyList<string> Taken);
