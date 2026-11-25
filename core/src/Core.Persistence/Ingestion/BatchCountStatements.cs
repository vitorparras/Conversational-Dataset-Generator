namespace Core.Persistence.Ingestion;

/// <summary>The statements of the numbers of a batch, apart from the reading that runs them.</summary>
internal static class BatchCountStatements
{
    /// <summary>The counts of the batch in the domain, by kind and code, from the function of the database that answers numbers alone (migration 0057).</summary>
    internal const string Read = "SELECT count_kind, count_code, count_value FROM public.batch_detail_counts(@batch, @domain)";

    /// <summary>
    /// The versions that sanitized the batch, the most recent first, what each of their steps did, in the order they ran, and how many items are sanitized out of how many, from the function
    /// that answers names and numbers alone; a batch with no sanitized item answers one row with no version.
    /// </summary>
    internal const string Steps =
        "SELECT sanitization_version, step_name, step_edits, items_changed, step_untreated, items_sanitized, items_total FROM public.batch_sanitization_counts(@batch, @domain)";

    /// <summary>How many markers of each kind the items of the batch hold, the authors apart, and in how many items, from the function that answers numbers alone (issue 101).</summary>
    internal const string Markers = "SELECT count_kind, count_markers, count_items FROM public.batch_marker_counts(@batch, @domain)";
}
