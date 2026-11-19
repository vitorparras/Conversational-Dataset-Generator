-- The numbers of the sanitization of a batch for its detail (issues 87 and 93), read while the batch is still open: the version of the list of steps that treated its items and, for each step in the
-- order it ran, how many edits it made and how many items it changed. The function answers the name of a step and numbers, never a word of the material nor a position, so the application reads them
-- without the scope of the system; the trail stays sealed. It runs as its owner, under the row policy of the domain the caller declared, and gives the scope back as it found it, as the 0057 does.
CREATE FUNCTION public.batch_sanitization_counts(of_batch bigint, of_domain integer)
    RETURNS TABLE (sanitization_version text, step_name text, step_edits bigint, items_changed bigint)
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
            SELECT i.sanitization_version AS version, i.sanitization_counts AS counts
            FROM content.item_text i
            WHERE i.batch_id = of_batch AND i.domain_id = of_domain AND i.emptied_at IS NULL AND i.sanitization_version IS NOT NULL),
        steps AS (
            SELECT v.version, s.step, s.position
            FROM content.sanitization_version v
            CROSS JOIN LATERAL pg_catalog.unnest(pg_catalog.string_to_array(v.steps, E'\n')) WITH ORDINALITY AS s (step, position)
            WHERE v.domain_id = of_domain AND v.version IN (SELECT items.version FROM items))
        SELECT steps.version, pg_catalog.split_part(steps.step, '@', 1),
               COALESCE(pg_catalog.sum(items.counts[steps.position]), 0)::bigint,
               pg_catalog.count(*) FILTER (WHERE items.counts[steps.position] > 0)
        FROM steps
        JOIN items ON items.version = steps.version
        GROUP BY steps.version, steps.step, steps.position
        ORDER BY steps.version, steps.position;
    PERFORM pg_catalog.set_config('app.content_scope', COALESCE(previous, ''), true);
END
$counts$;

REVOKE ALL ON FUNCTION public.batch_sanitization_counts(bigint, integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.batch_sanitization_counts(bigint, integer) TO app;
