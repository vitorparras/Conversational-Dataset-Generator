using Core.Ingestion;
using Core.Persistence.Content;
using Core.Platform.Keys;

namespace Core.Host.Submissions;

/// <summary>
/// The marking of the replicated content of a batch that comes in (issue 82): each message of a conversation is kept as the digests of its text and of its sender, never as the text or the sender,
/// and the criterion settles, against what the accepted batches left, which messages repeat a longer text of more than one sender. Nothing is removed or merged.
/// </summary>
internal sealed class ReplicationMarking
{
    private readonly ReplicationStore store;
    private readonly KeyDerivation derivation;
    private readonly ReplicationRule rule;

    /// <summary>The marking over the store of the digests, the derivation of the key and the criterion the configuration gave.</summary>
    /// <param name="store">Keeps the digests and the marks.</param>
    /// <param name="derivation">The derivation of the master key, which gives the digest of each domain.</param>
    /// <param name="rule">The size from which a text counts and the number of senders from which it is replicated.</param>
    internal ReplicationMarking(ReplicationStore store, KeyDerivation derivation, ReplicationRule rule)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(derivation);
        ArgumentNullException.ThrowIfNull(rule);
        this.store = store;
        this.derivation = derivation;
        this.rule = rule;
    }

    /// <summary>Writes the occurrences of the messages of the batch and the marks they bring, together; the items that carry no author or no moment are not messages and are left out.</summary>
    /// <param name="domainId">The domain of the batch.</param>
    /// <param name="batchId">The batch, still open.</param>
    /// <param name="items">Each unit of the conversation with the item that keeps its text, in the order they were written.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    internal async Task MarkAsync(int domainId, long batchId, IReadOnlyList<(long Item, ReadUnit Unit)> items, CancellationToken cancellationToken)
    {
        var arriving = items
            .Where(stored => stored.Unit.Author is not null && stored.Unit.Instant is not null)
            .Select((stored, rank) => ReplicationDigests.OccurrenceOf(
                derivation, domainId, new ReplicationPlace(stored.Item, 0), new Message(stored.Unit.Author!, stored.Unit.Instant!.Value, stored.Unit.RepliesTo, stored.Unit.Text, rank)))
            .ToList();
        var kept = await store.OccurrencesAsync(domainId, cancellationToken).ConfigureAwait(false);
        var known = kept.Select(found => new ReplicationOccurrence(new ReplicationPlace(found.Item, found.Position), found.TextDigest, found.SenderDigest, found.Length, found.SaidAt)).ToList();
        var marks = Replication.Marks(rule, known, arriving).Select(mark => new StoredMark(mark.Of.Item, mark.Of.Position, mark.First.Item, mark.First.Position)).ToList();
        var occurrences = arriving.Select(arrived => new StoredOccurrence(arrived.Place.Item, arrived.Place.Position, arrived.Text, arrived.Sender, arrived.Length, arrived.Instant)).ToList();
        await store.RecordAsync(domainId, batchId, occurrences, marks, cancellationToken).ConfigureAwait(false);
    }
}
