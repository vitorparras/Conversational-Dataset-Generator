using Core.Persistence.Content;
using Core.Persistence.Content.Writing;

namespace Core.Persistence.Ingestion;

/// <summary>
/// Adds the pseudonymized items of a batch (issue 101): a marker another row holds already is drawn again, without the row that holds it ever being read, and an entry of
/// the map that is not of an occurrence to pseudonymize is never sealed: the batch is rejected with the motive <see cref="BatchMotives.PseudonymOfRemoved" /> and its event.
/// </summary>
public sealed class PseudonymizedItems
{
    /// <summary>How many times the markers are drawn before the writing gives up; among a hundred million markers of each kind, a second loss is already rare.</summary>
    private const int Draws = 8;

    private readonly ContentWriter writer;
    private readonly IngestionBatches batches;

    /// <summary>The writing over the content and the batches.</summary>
    /// <param name="writer">Writes the item, its map and its markers together.</param>
    /// <param name="batches">Rejects the batch.</param>
    public PseudonymizedItems(ContentWriter writer, IngestionBatches batches)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(batches);
        (this.writer, this.batches) = (writer, batches);
    }

    /// <summary>Adds the item the pseudonymization gives, given the markers that are held already elsewhere, and draws again while some marker is lost.</summary>
    /// <param name="batch">The batch, its domain and who submitted the item, the author of a rejection.</param>
    /// <param name="pseudonymize">The item, pseudonymized with no marker among the ones given.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task<PseudonymizedItem> AddAsync(BatchClosing batch, Func<IReadOnlySet<string>, NewItemText> pseudonymize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(pseudonymize);
        return AddingAsync(batch, pseudonymize, cancellationToken);
    }

    private async Task<PseudonymizedItem> AddingAsync(BatchClosing batch, Func<IReadOnlySet<string>, NewItemText> pseudonymize, CancellationToken cancellationToken)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        for (var draw = 0; draw < Draws; draw++)
        {
            var item = pseudonymize(taken);
            if (item.Map is { } map && map.Any(entry => entry.Destination != OccurrenceDestination.Pseudonymize))
            {
                return new PseudonymizedItem(null, await batches.RejectAsync(batch, BatchMotives.PseudonymOfRemoved, cancellationToken).ConfigureAwait(false));
            }

            var written = await writer.AddPseudonymizedItemAsync(batch.DomainId, batch.BatchId, item, cancellationToken).ConfigureAwait(false);
            if (written.ItemId is { } itemId)
            {
                return new PseudonymizedItem(itemId, null);
            }

            taken.UnionWith(written.Taken);
        }

        throw new InvalidOperationException("Every draw of the markers lost to markers held already.");
    }
}
