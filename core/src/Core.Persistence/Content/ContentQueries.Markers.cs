namespace Core.Persistence.Content;

/// <summary>The queries of the markers an item holds and of the ones held in the installation (issue 101).</summary>
public static partial class ContentQueries
{
    /// <summary>
    /// Holds the markers of an item in the installation for its batch, the kind read from the word of the marker, and returns the ones the batch holds: the ones won now and the ones an
    /// earlier message of the same conversation won (0057). A marker another batch holds, of this domain or of another, is passed over and not returned, so whoever drew it learns it lost
    /// the draw without reading the row that won it; so is one another item holds in a batch that is no conversation, where each item counts as a conversation of its own.
    /// </summary>
    internal const string InsertMarkersInUse =
        "WITH won AS (INSERT INTO content.marker_in_use (domain_id, batch_id, marker, kind) SELECT @domain, @batch, m, CASE split_part(m, '_', 1) WHEN 'PESSOA' THEN 'person' "
        + "WHEN 'LUGAR' THEN 'place' WHEN 'ORGANIZACAO' THEN 'organization' END FROM unnest(@markers::text[]) AS t(m) ON CONFLICT DO NOTHING RETURNING marker) "
        + "SELECT marker FROM won UNION SELECT u.marker FROM content.marker_in_use u WHERE u.batch_id = @batch AND u.marker = ANY(@markers::text[]) "
        + "AND (EXISTS (SELECT 1 FROM content.ingestion_batch b WHERE b.id = @batch AND b.declared_form = 'conversation') "
        + "OR NOT EXISTS (SELECT 1 FROM content.item_marker i WHERE i.marker = u.marker))";

    /// <summary>Holds for the batch the markers given that no batch holds, and returns them: a marker held already, by this batch or another, is not among them.</summary>
    internal const string InsertMarkersWon =
        "INSERT INTO content.marker_in_use (domain_id, batch_id, marker, kind) SELECT @domain, @batch, m, CASE split_part(m, '_', 1) WHEN 'PESSOA' THEN 'person' "
        + "WHEN 'LUGAR' THEN 'place' WHEN 'ORGANIZACAO' THEN 'organization' END FROM unnest(@markers::text[]) AS t(m) ON CONFLICT DO NOTHING RETURNING marker";

    /// <summary>The batch of the item, whose conversation holds its markers.</summary>
    internal const string ReadItemBatch = "SELECT batch_id FROM content.item_text WHERE id = @item";

    /// <summary>Gives the item its markers, saying which stand for an author; a marker the item holds already is kept as it is.</summary>
    internal const string InsertItemMarkers =
        "INSERT INTO content.item_marker (domain_id, item_id, marker, author) SELECT @domain, @item, m, a FROM unnest(@markers::text[], @authors::boolean[]) AS t(m, a) "
        + "ON CONFLICT (item_id, marker) DO NOTHING";

    /// <summary>Takes from the item the markers its text no longer holds; they stay held in the installation, so none comes back in another conversation.</summary>
    internal const string RemoveItemMarkersGone = "DELETE FROM content.item_marker WHERE item_id = @item AND marker <> ALL(@markers::text[])";

    /// <summary>Removes the markers of the items of a batch just rejected, before its maps and its items; the markers stay held in the installation.</summary>
    internal const string RemoveBatchItemMarkers = "DELETE FROM content.item_marker m USING content.item_text i WHERE i.id = m.item_id AND i.batch_id = @id";

    /// <summary>Empties the markers of an item, with the item; they stay held in the installation.</summary>
    internal const string EmptyItemMarkers = "DELETE FROM content.item_marker WHERE item_id = @id";

    /// <summary>
    /// Whether some item of the batch holds a pseudonymized text, asked when it is accepted, so that the event of the pseudonymization goes with the acceptance; the database answers by the
    /// function its trigger of the acceptance asks, in the domain of the transaction; with no domain declared it returns no row.
    /// </summary>
    internal const string ReadBatchPseudonymized = "SELECT public.batch_holds_pseudonymized(@id, d) FROM public.declared_domain() AS d WHERE d IS NOT NULL";
}
