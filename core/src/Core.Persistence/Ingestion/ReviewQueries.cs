namespace Core.Persistence.Ingestion;

/// <summary>The statements of the review of a batch by sample; the database refuses every one of them without the event of the change in the same transaction.</summary>
internal static class ReviewQueries
{
    /// <summary>Takes the turn of the batch, so that no item is added between the numbers being read and the sample being drawn.</summary>
    internal const string TakeTurn = "SELECT public.take_batch_turn(@id)";

    /// <summary>The numbers of the items of the batch, in order; the numbers only, never what the items hold.</summary>
    internal const string ItemNumbers = "SELECT coalesce(array_agg(item ORDER BY item), '{}'::bigint[]) FROM public.batch_item_ids(@id, @domain) AS item";
}
