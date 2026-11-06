namespace Core.Persistence.Custody;

public sealed partial record CustodyEvent
{
    private const int MaxMotive = 63;

    /// <summary>The event of a batch of ingestion opened by a person, who must be active; the item is the batch, and nothing of what it will hold travels in the event.</summary>
    /// <param name="personId">The person who opened the batch.</param>
    /// <param name="batchId">The identifier the batch took.</param>
    public static CustodyEvent BatchOpened(long personId, long batchId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchId);
        return new CustodyEvent(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.IngestionBatch, batchId), CustodyStep.BatchOpened, CustodyReason.AdministrativeAct);
    }

    /// <summary>The event of a batch accepted, by a person or by the system; the item is the batch, and the event has no reference.</summary>
    /// <param name="author">Who accepted the batch.</param>
    /// <param name="batchId">The batch.</param>
    public static CustodyEvent BatchAccepted(CustodyAuthor author, long batchId)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchId);
        return new CustodyEvent(author, new CustodyItem(CustodyItemKind.IngestionBatch, batchId), CustodyStep.BatchClosed, CustodyReason.BatchAccepted);
    }

    /// <summary>The event of a batch rejected, by a person or by the system; the item is the batch and the reference is the motive, a name in snake_case and never free text.</summary>
    /// <param name="author">Who rejected the batch.</param>
    /// <param name="batchId">The batch.</param>
    /// <param name="motive">The motive of the rejection; see <see cref="IsBatchMotive" />.</param>
    public static CustodyEvent BatchRejected(CustodyAuthor author, long batchId, string motive)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchId);
        return IsBatchMotive(motive)
            ? new CustodyEvent(author, new CustodyItem(CustodyItemKind.IngestionBatch, batchId), CustodyStep.BatchClosed, CustodyReason.BatchRejected) { Reference = motive }
            : throw new ArgumentException("The motive of a rejection is a name of lowercase letters, digits and underscores, starting with a letter, up to 63 characters.", nameof(motive));
    }

    /// <summary>Whether the text is a motive the database takes: a lowercase letter, then lowercase letters, digits and underscores, up to 63 characters in all.</summary>
    /// <param name="motive">The text to judge.</param>
    public static bool IsBatchMotive(string? motive) =>
        motive is { Length: > 0 and <= MaxMotive } && char.IsAsciiLetterLower(motive[0])
        && motive.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_');
}
