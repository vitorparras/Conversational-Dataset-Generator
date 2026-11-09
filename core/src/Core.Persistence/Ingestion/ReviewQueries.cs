namespace Core.Persistence.Ingestion;

/// <summary>The statements of the review of a batch by sample; the database refuses every one of them without the event of the change in the same transaction.</summary>
internal static class ReviewQueries
{
    /// <summary>Takes the turn of the batch, so that no item is added between the numbers being read and the sample being drawn.</summary>
    internal const string TakeTurn = "SELECT public.take_batch_turn(@id)";

    /// <summary>The numbers of the items of the batch, in order; the numbers only, never what the items hold.</summary>
    internal const string ItemNumbers = "SELECT coalesce(array_agg(item ORDER BY item), '{}'::bigint[]) FROM public.batch_item_ids(@id, @domain) AS item";

    /// <summary>The state, the sample and the review of the batch of the domain declared, whatever its state.</summary>
    internal const string Read =
        "SELECT state, review_sample, sampled_at, reviewed_by, reviewed_at, review_finding FROM content.ingestion_batch WHERE id = @id";

    /// <summary>Writes the sample of an open batch that has none.</summary>
    internal const string Draw = "UPDATE content.ingestion_batch SET review_sample = @sample WHERE id = @id AND state = 'open' AND review_sample IS NULL";

    /// <summary>Records a review that found nothing, for an open batch whose sample was drawn and not reviewed.</summary>
    internal const string ReviewClean =
        "UPDATE content.ingestion_batch SET reviewed_by = @reviewer WHERE id = @id AND state = 'open' AND review_sample IS NOT NULL AND reviewed_by IS NULL";

    /// <summary>Records a review that found something and rejects the batch with it, in the one statement.</summary>
    internal const string ReviewFound =
        "UPDATE content.ingestion_batch SET reviewed_by = @reviewer, review_finding = @finding, state = 'rejected', motive = @motive "
        + "WHERE id = @id AND state = 'open' AND review_sample IS NOT NULL AND reviewed_by IS NULL";
}
