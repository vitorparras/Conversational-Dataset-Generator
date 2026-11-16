-- The numbers of a batch for its detail (issue 821), read while the batch is still open, when the items, the occurrences and the marks are visible to the system alone: how many items took each form,
-- the declared one or the one each accommodated item received; how many accommodated items declared each loss; how many passages go to each destination; and how many messages were marked as a
-- repetition of another and how many first messages they repeat. The function answers numbers and codes and never a word of the material, so the application reads them without the scope of the
-- system; it runs as its owner, under the row policy of the domain the caller declared, and gives the scope back as it found it.
CREATE FUNCTION public.batch_detail_counts(of_batch bigint, of_domain integer) RETURNS TABLE (count_kind text, count_code text, count_value bigint)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS $counts$
DECLARE
    previous text := pg_catalog.current_setting('app.content_scope', true);
BEGIN
    PERFORM pg_catalog.set_config('app.content_scope', 'system', true);
    RETURN QUERY
        WITH items AS (
            SELECT COALESCE(i.accommodated_into, b.declared_form) AS form, i.accommodated_into, i.losses, i.removed_passages, i.pseudonymized_passages, i.preserved_passages
            FROM content.item_text i
            JOIN content.ingestion_batch b ON b.id = i.batch_id AND b.domain_id = i.domain_id
            WHERE i.batch_id = of_batch AND i.domain_id = of_domain AND i.emptied_at IS NULL),
        marks AS (
            SELECT m.first_item_id, m.first_message_position FROM content.replication_mark m WHERE m.batch_id = of_batch AND m.domain_id = of_domain)
        SELECT 'form'::text, items.form, pg_catalog.count(*) FROM items WHERE items.form IS NOT NULL GROUP BY items.form
        UNION ALL
        SELECT 'loss'::text, items.accommodated_into || ':' || items.losses, pg_catalog.count(*) FROM items WHERE items.accommodated_into IS NOT NULL GROUP BY items.accommodated_into, items.losses
        UNION ALL
        SELECT 'destination'::text, 'remove'::text, COALESCE(pg_catalog.sum(items.removed_passages), 0)::bigint FROM items
        UNION ALL
        SELECT 'destination'::text, 'pseudonymize'::text, COALESCE(pg_catalog.sum(items.pseudonymized_passages), 0)::bigint FROM items
        UNION ALL
        SELECT 'destination'::text, 'preserve'::text, COALESCE(pg_catalog.sum(items.preserved_passages), 0)::bigint FROM items
        UNION ALL
        SELECT 'replica'::text, 'marked'::text, pg_catalog.count(*) FROM marks
        UNION ALL
        SELECT 'replica'::text, 'firsts'::text, pg_catalog.count(DISTINCT (marks.first_item_id, marks.first_message_position)) FROM marks;
    PERFORM pg_catalog.set_config('app.content_scope', COALESCE(previous, ''), true);
END
$counts$;

REVOKE ALL ON FUNCTION public.batch_detail_counts(bigint, integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.batch_detail_counts(bigint, integer) TO app;
