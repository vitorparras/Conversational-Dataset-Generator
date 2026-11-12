using Core.Persistence.Custody;

namespace Core.Persistence.Content.Reading;

/// <summary>
/// How a stage after the ingestion learns what was decided for an item: it reads the destination that was stored with the item, through the one way protected content is read, and decides nothing.
/// An item for which no decision was stored answers as empty, never with a default destination.
/// </summary>
public sealed class ItemOccurrenceReader
{
    private readonly ProtectedContentReader reader;

    /// <summary>The reader of the occurrences over the one reader of protected content.</summary>
    /// <param name="reader">The reader that records the reading in the chain before it opens anything.</param>
    public ItemOccurrenceReader(ProtectedContentReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        this.reader = reader;
    }

    /// <summary>The occurrences stored with an item and the destination chosen for each, recording the reading before they are opened.</summary>
    /// <param name="author">Who reads; the event of the reading names them.</param>
    /// <param name="domainId">The domain the item belongs to.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<ItemOccurrencesRead> ReadAsync(CustodyAuthor author, int domainId, long itemId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(author);
        return ReadingAsync(author, domainId, itemId, cancellationToken);
    }

    private async Task<ItemOccurrencesRead> ReadingAsync(CustodyAuthor author, int domainId, long itemId, CancellationToken cancellationToken)
    {
        var read = await reader.ReadAsync(new ContentRead(author, ContentColumns.Occurrences, domainId, itemId), cancellationToken).ConfigureAwait(false);
        return new ItemOccurrencesRead(read, read.Outcome == ContentReadOutcome.Read ? OccurrenceJson.Read(read.Text!) : null);
    }
}
