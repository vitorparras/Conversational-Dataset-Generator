-- The review by sample reads only the pseudonymized text of an item (issue 939). The door of 0059 opened the sanitized text, or the raw one, of an item of the sample of an open batch; from here
-- it opens the pseudonymized text and nothing else, under a reading step of its own, pseudonymized_text_read, which the chain lets in while it is broken, as it lets in every other reading.
-- An item of the sample without the pseudonymized text takes no reading at all: the step answers pseudonymized_text_absent, so that the review says why it shows nothing, and the text is never opened.
-- The third function tells whether every item of the sample up to a place has the pseudonymized text, so that such an item is neither marked as reviewed nor let into an approval.

ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'origin_read', 'configuration_approved', 'configuration_refused',
         'configuration_in_force', 'person_email_corrected', 'configuration_not_applied', 'occurrences_read', 'batch_sampled', 'batch_reviewed', 'batch_sanitized',
         'sanitization_rerun', 'item_resanitized', 'batch_pseudonymized',
         'reversal_requested', 'reversal_request_refused', 'reversal_request_read', 'reversal_authorized', 'reversal_authorization_refused', 'reversal_declined',
         'reversal_revealed', 'reversal_denied', 'pseudonymized_text_read'));

CREATE OR REPLACE FUNCTION public.custody_event_link() RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    tip_sequence bigint;
    tip_digest bytea;
    author_active boolean;
BEGIN
    IF NEW.step = 'verify_chain' AND NEW.reason = 'verification_passed' AND public.custody_chain_broken() THEN
        RAISE EXCEPTION 'the custody chain failed its last verification: a verdict that passed is not added until the chain is restored'
            USING ERRCODE = 'CE003', SCHEMA = 'public', TABLE = 'custody_event';
    END IF;

    IF NEW.step NOT IN ('verify_chain', 'raw_text_read', 'sanitized_text_read', 'pseudonymized_text_read', 'reversal_entries_read', 'origin_read', 'occurrences_read',
                        'sign_in_accepted', 'sign_in_refused', 'credential_recomputed')
        AND public.custody_chain_broken() THEN
        RAISE EXCEPTION 'the custody chain failed its last verification: only a verification, a reading or an entry is added until the chain is restored'
            USING ERRCODE = 'CE002', SCHEMA = 'public', TABLE = 'custody_event';
    END IF;

    IF NEW.actor_person_id IS NOT NULL THEN
        SELECT p.active INTO author_active FROM public.person p WHERE p.id = NEW.actor_person_id;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'the author of a custody event is not a registered person'
                USING ERRCODE = 'foreign_key_violation', CONSTRAINT = 'custody_event_actor_fkey';
        ELSIF NOT author_active THEN
            RAISE EXCEPTION 'the author of a custody event must be an active person'
                USING ERRCODE = 'check_violation', CONSTRAINT = 'custody_event_actor_active';
        END IF;
    END IF;

    IF NEW.corrects IS NOT NULL AND NOT EXISTS (SELECT 1 FROM public.custody_event c WHERE c.sequence = NEW.corrects) THEN
        RAISE EXCEPTION 'the event to correct is not in the chain'
            USING ERRCODE = 'foreign_key_violation', CONSTRAINT = 'custody_event_corrects_fkey';
    END IF;

    SELECT e.sequence, e.digest INTO tip_sequence, tip_digest FROM public.custody_event e ORDER BY e.sequence DESC LIMIT 1;
    NEW.sequence := COALESCE(tip_sequence, 0) + 1;
    NEW.previous_digest := COALESCE(tip_digest, public.custody_origin());
    NEW.digest := public.custody_digest(
        NEW.previous_digest, NEW.sequence, NEW.occurred_at, NEW.actor_person_id, NEW.actor_system,
        NEW.item_kind, NEW.item_id, NEW.step, NEW.reason, NEW.corrects, NEW.recorded_by, NEW.reference);
    RETURN NEW;
END
$$;

CREATE OR REPLACE FUNCTION public.batch_sample_step(of_batch bigint, of_domain integer, of_item bigint) RETURNS text
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS $step$
DECLARE
    previous text := pg_catalog.current_setting('app.content_scope', true);
    taken text;
BEGIN
    PERFORM pg_catalog.set_config('app.content_scope', 'system', true);
    SELECT CASE WHEN i.pseudonymized_text IS NOT NULL THEN 'pseudonymized_text_read' ELSE 'pseudonymized_text_absent' END INTO taken
    FROM content.item_text i
    JOIN content.ingestion_batch b ON b.id = i.batch_id AND b.domain_id = i.domain_id
    WHERE i.id = of_item AND i.batch_id = of_batch AND i.domain_id = of_domain AND i.emptied_at IS NULL
      AND b.state = 'open' AND b.review_sample IS NOT NULL AND b.reviewed_by IS NULL
      AND of_item = ANY (pg_catalog.string_to_array(b.review_sample, ',')::pg_catalog.int8[]);
    PERFORM pg_catalog.set_config('app.content_scope', COALESCE(previous, ''), true);
    RETURN taken;
END
$step$;

CREATE OR REPLACE FUNCTION public.batch_sample_text(of_batch bigint, of_domain integer, of_item bigint) RETURNS TABLE (read_step text, sealed_text bytea, key_epoch integer, key_wrapped bytea)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS $text$
DECLARE
    previous text := pg_catalog.current_setting('app.content_scope', true);
BEGIN
    PERFORM pg_catalog.set_config('app.content_scope', 'system', true);
    RETURN QUERY
        SELECT 'pseudonymized_text_read'::text, i.pseudonymized_text, i.dek_epoch, i.dek_wrapped
        FROM content.item_text i
        JOIN content.ingestion_batch b ON b.id = i.batch_id AND b.domain_id = i.domain_id
        WHERE i.id = of_item AND i.batch_id = of_batch AND i.domain_id = of_domain AND i.emptied_at IS NULL
          AND b.state = 'open' AND b.review_sample IS NOT NULL AND b.reviewed_by IS NULL
          AND of_item = ANY (pg_catalog.string_to_array(b.review_sample, ',')::pg_catalog.int8[])
          AND i.pseudonymized_text IS NOT NULL
          AND public.custody_event_of_this_transaction('item_text', i.id, 'pseudonymized_text_read');
    PERFORM pg_catalog.set_config('app.content_scope', COALESCE(previous, ''), true);
END
$text$;

-- Whether every item of the sample of the batch, from the first place to up_to, is there and has the pseudonymized text; an item emptied since is no text to show and is not held against the review.
CREATE FUNCTION public.batch_sample_pseudonymized(of_batch bigint, of_domain integer, up_to integer) RETURNS boolean
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS $whole$
DECLARE
    previous text := pg_catalog.current_setting('app.content_scope', true);
    whole boolean;
BEGIN
    PERFORM pg_catalog.set_config('app.content_scope', 'system', true);
    SELECT NOT EXISTS (
        SELECT 1
        FROM content.ingestion_batch b
        CROSS JOIN LATERAL pg_catalog.unnest(pg_catalog.string_to_array(b.review_sample, ',')::pg_catalog.int8[]) WITH ORDINALITY AS s(item, place)
        LEFT JOIN content.item_text i ON i.id = s.item AND i.batch_id = b.id AND i.domain_id = b.domain_id
        WHERE b.id = of_batch AND b.domain_id = of_domain AND s.place <= up_to
          AND (i.id IS NULL OR (i.pseudonymized_text IS NULL AND i.emptied_at IS NULL))) INTO whole;
    PERFORM pg_catalog.set_config('app.content_scope', COALESCE(previous, ''), true);
    RETURN whole;
END
$whole$;

REVOKE ALL ON FUNCTION public.batch_sample_pseudonymized(bigint, integer, integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.batch_sample_pseudonymized(bigint, integer, integer) TO app;
