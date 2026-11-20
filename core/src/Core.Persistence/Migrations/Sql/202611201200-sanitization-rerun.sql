-- The rerun of the sanitization over the raw text (issue 97): a request queues the accepted items of another version, and a worker treats each in one transaction with its event and
-- its conclusion, the only one in which its sanitization may change. The queue holds identifiers, states, moments and codes, never a word of the material.

-- What each step left untreated, one number for each step of the version, as the counts are; null in the items written before the step. The emptying clears it with the rest.
ALTER TABLE content.item_text
    ADD COLUMN sanitization_untreated integer[],
    ADD CONSTRAINT item_text_sanitization_untreated CHECK (sanitization_untreated IS NULL
        OR (sanitization_counts IS NOT NULL AND pg_catalog.cardinality(sanitization_untreated) = pg_catalog.cardinality(sanitization_counts)
            AND pg_catalog.array_position(sanitization_untreated, NULL) IS NULL AND 0 <= ALL (sanitization_untreated))),
    DROP CONSTRAINT item_text_emptied,
    ADD CONSTRAINT item_text_emptied CHECK ((emptied_at IS NULL AND raw_text IS NOT NULL)
        OR (emptied_at IS NOT NULL AND raw_text IS NULL AND sanitized_text IS NULL AND pseudonymized_text IS NULL AND occurrences IS NULL
            AND removed_passages = 0 AND pseudonymized_passages = 0 AND preserved_passages = 0
            AND sanitization_version IS NULL AND sanitization_trail IS NULL AND sanitization_counts IS NULL AND sanitization_untreated IS NULL));

GRANT UPDATE (sanitized_text, sanitization_untreated) ON content.item_text TO app;

-- The request: the version it reruns to, who asked and when. It is open while one of its items is not concluded.
SELECT public.create_content_table('sanitization_rerun',
    $columns$target_version text NOT NULL,
     requested_by bigint NOT NULL REFERENCES public.person (id),
     requested_at timestamptz NOT NULL DEFAULT pg_catalog.now(),
     CONSTRAINT sanitization_rerun_target FOREIGN KEY (target_version, domain_id) REFERENCES content.sanitization_version (version, domain_id),
     CONSTRAINT sanitization_rerun_id_domain UNIQUE (id, domain_id)$columns$);

-- One item of a request: queued, held by a worker until a moment, or done with its result; a refusal says why. An item is in one open request at most.
SELECT public.create_content_table('sanitization_rerun_item',
    $columns$rerun_id bigint NOT NULL,
     item_id bigint NOT NULL,
     state text NOT NULL DEFAULT 'queued',
     hold_until timestamptz,
     tries integer NOT NULL DEFAULT 0,
     concluded_at timestamptz,
     result text,
     refusal text,
     CONSTRAINT sanitization_rerun_item_rerun FOREIGN KEY (rerun_id, domain_id) REFERENCES content.sanitization_rerun (id, domain_id),
     CONSTRAINT sanitization_rerun_item_item FOREIGN KEY (item_id, domain_id) REFERENCES content.item_text (id, domain_id),
     CONSTRAINT sanitization_rerun_item_once UNIQUE (rerun_id, item_id),
     CONSTRAINT sanitization_rerun_item_state CHECK (state IN ('queued', 'held', 'done')),
     CONSTRAINT sanitization_rerun_item_result CHECK (result IN ('identical', 'replaced', 'refused', 'superseded')),
     CONSTRAINT sanitization_rerun_item_refusal CHECK (refusal IN ('emptied', 'key_lost', 'failed')),
     CONSTRAINT sanitization_rerun_item_tries CHECK (tries >= 0),
     CONSTRAINT sanitization_rerun_item_shape CHECK ((state = 'done') = (result IS NOT NULL) AND (state = 'done') = (concluded_at IS NOT NULL)
         AND (state = 'held') = (hold_until IS NOT NULL) AND (refusal IS NOT NULL) = (result IS NOT DISTINCT FROM 'refused'))$columns$);

CREATE UNIQUE INDEX sanitization_rerun_item_open ON content.sanitization_rerun_item (item_id) WHERE state <> 'done';

GRANT SELECT ON content.sanitization_rerun, content.sanitization_rerun_item TO app;
GRANT INSERT (domain_id, target_version, requested_by, requested_at) ON content.sanitization_rerun TO app;
GRANT INSERT (domain_id, rerun_id, item_id) ON content.sanitization_rerun_item TO app;
GRANT UPDATE (state, hold_until, tries, concluded_at, result, refusal) ON content.sanitization_rerun_item TO app;

-- A row not emptied keeps its texts whoever writes (the 0061 as it was, without the sanitization): the sanitization moves to the trigger below, which lets it change only with a rerun.
DROP TRIGGER emptied_content_is_final ON content.item_text;
CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL
        OR (NEW.emptied_at IS NULL
            AND (NEW.raw_text IS DISTINCT FROM OLD.raw_text OR NEW.pseudonymized_text IS DISTINCT FROM OLD.pseudonymized_text
                OR NEW.occurrences IS DISTINCT FROM OLD.occurrences
                OR NEW.removed_passages IS DISTINCT FROM OLD.removed_passages OR NEW.pseudonymized_passages IS DISTINCT FROM OLD.pseudonymized_passages
                OR NEW.preserved_passages IS DISTINCT FROM OLD.preserved_passages)))
    EXECUTE FUNCTION public.refuse_change_of_emptied_content();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER emptied_content_is_final;

-- The sanitization of an item not emptied changes only in the transaction that wrote the event item_resanitized of the item and concluded the item in the queue (both rows carry this transaction as
-- xmin), for every role, the owner included. The refusal is the one of a content changed outside its path (CE004) and names the table and no value; an emptied row is refused by the trigger above.
CREATE FUNCTION public.refuse_resanitization_without_rerun() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NOT public.custody_event_of_this_transaction('item_text', NEW.id, 'item_resanitized')
        OR NOT EXISTS (SELECT 1 FROM content.sanitization_rerun_item q
                       WHERE q.item_id = NEW.id AND q.domain_id = NEW.domain_id AND q.state = 'done' AND q.xmin = pg_catalog.pg_current_xact_id()::pg_catalog.xid) THEN
        RAISE EXCEPTION 'the sanitization of an item changes only with the event of its rerun and its conclusion in the queue, in the same transaction'
            USING ERRCODE = 'CE004', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER resanitization_needs_rerun BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NULL AND NEW.emptied_at IS NULL
        AND (NEW.sanitized_text IS DISTINCT FROM OLD.sanitized_text OR NEW.sanitization_version IS DISTINCT FROM OLD.sanitization_version
            OR NEW.sanitization_trail IS DISTINCT FROM OLD.sanitization_trail OR NEW.sanitization_counts IS DISTINCT FROM OLD.sanitization_counts
            OR NEW.sanitization_untreated IS DISTINCT FROM OLD.sanitization_untreated))
    EXECUTE FUNCTION public.refuse_resanitization_without_rerun();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER resanitization_needs_rerun;

-- One count for each step of the version on the update too, so that a rerun cannot store counts of another list.
DROP TRIGGER sanitization_counts_match_steps ON content.item_text;
CREATE TRIGGER sanitization_counts_match_steps AFTER INSERT OR UPDATE OF sanitization_version, sanitization_counts ON content.item_text
    FOR EACH ROW WHEN (NEW.sanitization_version IS NOT NULL) EXECUTE FUNCTION public.refuse_counts_not_matching_steps();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER sanitization_counts_match_steps;

-- The chain learns the step sanitization_rerun, one for each request, by the person who asked, about the request, and item_resanitized, one for each item concluded, by the system, about the item,
-- with the result as the reason. Both carry the version the request reruns to as the reference, and never a word of the content. The checks change from the ones the 0041, the 0054 and the 0062 left.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_reason_listed,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN
        ('person', 'item_text', 'reversal_map', 'custody_chain', 'content_epoch', 'domain', 'sign_in_attempt', 'master_key', 'ingestion_batch', 'configuration_change',
         'sanitization_rerun')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'origin_read', 'configuration_approved', 'configuration_refused',
         'configuration_in_force', 'person_email_corrected', 'configuration_not_applied', 'occurrences_read', 'batch_sampled', 'batch_reviewed', 'batch_sanitized',
         'sanitization_rerun', 'item_resanitized')),
    ADD CONSTRAINT custody_event_reason_listed CHECK (reason IN
        ('administrative_act', 'correction_of_record', 'content_read', 'verification_passed', 'verification_failed',
         'removal_requested_by_subject', 'contract_requirement', 'author_without_function', 'item_already_emptied',
         'credentials_accepted', 'password_wrong', 'account_not_recognized',
         'batch_accepted', 'batch_rejected', 'review_clean', 'review_found',
         'sanitization_identical', 'sanitization_replaced', 'sanitization_refused', 'sanitization_superseded')),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (step IN ('recovery_tested', 'verify_chain')
            OR (reference IS NOT NULL)
                = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored',
                            'restore_rehearsed', 'domain_label_revoked', 'password_changed', 'configuration_approved', 'configuration_refused', 'configuration_in_force',
                            'configuration_not_applied', 'batch_sanitized', 'sanitization_rerun', 'item_resanitized')
                    OR reason = 'batch_rejected'))
        AND (step <> 'verify_chain' OR reference IS NULL OR reference ~ '^requested-by/[1-9][0-9]{0,17}$')
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL)),
    ADD CONSTRAINT custody_event_rerun_shape CHECK (
        (step = 'sanitization_rerun') = (item_kind = 'sanitization_rerun')
        AND (step = 'item_resanitized') = (reason IN ('sanitization_identical', 'sanitization_replaced', 'sanitization_refused', 'sanitization_superseded'))
        AND (step <> 'sanitization_rerun' OR (reason = 'administrative_act' AND actor_person_id IS NOT NULL AND corrects IS NULL AND reference ~ '^[0-9a-f]{64}$'))
        AND (step <> 'item_resanitized' OR (item_kind = 'item_text' AND actor_system AND corrects IS NULL AND reference ~ '^[0-9a-f]{64}$')));

-- The numbers of the sanitization of a batch (the 0063 with three columns more): items sanitized and items not emptied on every row, and what each step left untreated; the most recent
-- version first, and a batch with no sanitized item still answers one row, with no version, so that its items are counted.
DROP FUNCTION public.batch_sanitization_counts(bigint, integer);
CREATE FUNCTION public.batch_sanitization_counts(of_batch bigint, of_domain integer)
    RETURNS TABLE (sanitization_version text, step_name text, step_edits bigint, items_changed bigint, step_untreated bigint, items_sanitized bigint, items_total bigint)
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
            SELECT i.sanitization_version AS version, i.sanitization_counts AS counts, i.sanitization_untreated AS untreated
            FROM content.item_text i
            WHERE i.batch_id = of_batch AND i.domain_id = of_domain AND i.emptied_at IS NULL),
        totals AS (
            SELECT pg_catalog.count(items.version) AS sanitized, pg_catalog.count(*) AS total FROM items),
        steps AS (
            SELECT v.id, v.version, s.step, s.position
            FROM content.sanitization_version v
            CROSS JOIN LATERAL pg_catalog.unnest(pg_catalog.string_to_array(v.steps, E'\n')) WITH ORDINALITY AS s (step, position)
            WHERE v.domain_id = of_domain AND v.version IN (SELECT items.version FROM items))
        SELECT steps.version, pg_catalog.split_part(steps.step, '@', 1),
               COALESCE(pg_catalog.sum(items.counts[steps.position]), 0)::bigint,
               pg_catalog.count(*) FILTER (WHERE items.counts[steps.position] > 0),
               COALESCE(pg_catalog.sum(items.untreated[steps.position]), 0)::bigint,
               totals.sanitized, totals.total
        FROM totals
        LEFT JOIN (steps JOIN items ON items.version = steps.version) ON true
        GROUP BY steps.id, steps.version, steps.step, steps.position, totals.sanitized, totals.total
        ORDER BY steps.id DESC, steps.position;
    PERFORM pg_catalog.set_config('app.content_scope', COALESCE(previous, ''), true);
END
$counts$;

REVOKE ALL ON FUNCTION public.batch_sanitization_counts(bigint, integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.batch_sanitization_counts(bigint, integer) TO app;
