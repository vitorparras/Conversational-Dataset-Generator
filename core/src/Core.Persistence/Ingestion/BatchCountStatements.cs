namespace Core.Persistence.Ingestion;

/// <summary>The statements of the numbers of a batch, apart from the reading that runs them.</summary>
internal static class BatchCountStatements
{
    /// <summary>The counts of the batch in the domain, by kind and code, from the function of the database that answers numbers alone (migration 0057).</summary>
    internal const string Read = "SELECT count_kind, count_code, count_value FROM public.batch_detail_counts(@batch, @domain)";
}
