namespace Core.Persistence.Custody;

/// <summary>The last event of the chain at a moment: its sequence, zero when the chain was empty, and its digest.</summary>
/// <param name="Sequence">The sequence of the event.</param>
/// <param name="Digest">The digest of the event; null when the chain was empty.</param>
internal sealed record ChainTip(long Sequence, byte[]? Digest)
{
    /// <summary>The tip of a chain with no event, or of a database with no chain.</summary>
    internal static ChainTip None { get; } = new(0, null);
}
