namespace Core.Persistence.Ingestion;

/// <summary>The statements of the review of a batch by sample; the database refuses every one of them without the event of the change in the same transaction.</summary>
internal static class ReviewQueries
{
    internal const string TakeTurn = "SELECT public.take_batch_turn(@id)";
    internal const string ItemNumbers = "SELECT coalesce(array_agg(item ORDER BY item), '{}'::bigint[]) FROM public.batch_item_ids(@id, @domain) AS item";
    internal const string Read = "SELECT state, review_sample, sampled_at FROM content.ingestion_batch WHERE id = @id";
    internal const string Draw = "UPDATE content.ingestion_batch SET review_sample = @sample WHERE id = @id AND state = 'open' AND review_sample IS NULL";
}
