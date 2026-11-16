-- The text of an item of the sample for its review (issue 822). The row policy hides the items of an open batch from everyone but the transaction that wrote them, and the review reads them while the batch is
-- still open, so this function opens exactly that door and no wider: the item must belong to the batch and the domain, be listed in the sample the batch drew, not be emptied, and the batch must be open and
-- sampled and not yet reviewed. It answers the sealed text the review shows, the sanitized one when the item has it and the raw one when it has not, with the key that opens it, and only when this very transaction
-- already wrote the event of that reading, so that no text leaves without the chain saying who read which item. It answers nothing otherwise. It runs as its owner, under the row policy of the domain the caller
-- declared, and gives the scope back as it found it.
CREATE FUNCTION public.batch_sample_text(of_batch bigint, of_domain integer, of_item bigint) RETURNS TABLE (read_step text, sealed_text bytea, key_epoch integer, key_wrapped bytea)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS $text$
DECLARE
    previous text := pg_catalog.current_setting('app.content_scope', true);
BEGIN
    PERFORM pg_catalog.set_config('app.content_scope', 'system', true);
    RETURN QUERY
        SELECT CASE WHEN i.sanitized_text IS NOT NULL THEN 'sanitized_text_read' ELSE 'raw_text_read' END,
               COALESCE(i.sanitized_text, i.raw_text), i.dek_epoch, i.dek_wrapped
        FROM content.item_text i
        JOIN content.ingestion_batch b ON b.id = i.batch_id AND b.domain_id = i.domain_id
        WHERE i.id = of_item AND i.batch_id = of_batch AND i.domain_id = of_domain AND i.emptied_at IS NULL
          AND b.state = 'open' AND b.review_sample IS NOT NULL AND b.reviewed_by IS NULL
          AND of_item = ANY (pg_catalog.string_to_array(b.review_sample, ',')::pg_catalog.int8[])
          AND COALESCE(i.sanitized_text, i.raw_text) IS NOT NULL
          AND public.custody_event_of_this_transaction('item_text', i.id, CASE WHEN i.sanitized_text IS NOT NULL THEN 'sanitized_text_read' ELSE 'raw_text_read' END);
    PERFORM pg_catalog.set_config('app.content_scope', COALESCE(previous, ''), true);
END
$text$;

REVOKE ALL ON FUNCTION public.batch_sample_text(bigint, integer, bigint) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.batch_sample_text(bigint, integer, bigint) TO app;
