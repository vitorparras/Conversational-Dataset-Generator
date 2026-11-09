-- The sample of a batch for review (issue 83): drawn once by the system, with its event in the chain, and never changed after. The chain learns the step batch_sampled, by the person who asks for the
-- review; the checks change from the last ones the earlier steps left (the steps from the 0044, the shape of the batch from the 0042), and everything they list stays.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_batch_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'origin_read', 'occurrences_read', 'batch_sampled')),
    ADD CONSTRAINT custody_event_batch_shape CHECK (
        (step IN ('batch_opened', 'batch_closed', 'batch_sampled', 'origin_read')) = (item_kind = 'ingestion_batch')
        AND (step = 'batch_closed') = (reason IN ('batch_accepted', 'batch_rejected'))
        AND (step NOT IN ('batch_opened', 'batch_sampled') OR (reason = 'administrative_act' AND actor_person_id IS NOT NULL))
        AND (step <> 'origin_read' OR (reason = 'content_read' AND reference IS NULL))
        AND (step NOT IN ('batch_opened', 'batch_closed', 'batch_sampled', 'origin_read') OR corrects IS NULL)
        AND (step <> 'batch_closed' OR (reason = 'batch_rejected') = (reference IS NOT NULL))
        AND (step <> 'batch_closed' OR reference IS NULL OR reference ~ '^[a-z][a-z0-9_]{0,62}$'));

-- The sample in the row of the batch: the numbers of the items (empty for a batch with no item) and the server's moment of the draw.
ALTER TABLE content.ingestion_batch
    ADD COLUMN review_sample text,
    ADD COLUMN sampled_at timestamptz,
    ADD CONSTRAINT ingestion_batch_sample CHECK ((review_sample IS NULL) = (sampled_at IS NULL) AND (review_sample IS NULL OR review_sample ~ '^([0-9]+(,[0-9]+)*)?$'));

GRANT UPDATE (review_sample) ON content.ingestion_batch TO app;

-- The numbers of the items of an open batch, which the row policy hides from whoever did not write them in this very transaction: the numbers only, never what the items hold. The sample is drawn from them.
CREATE FUNCTION public.batch_item_ids(of_batch bigint, of_domain integer) RETURNS SETOF bigint
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog
AS $ids$
    SELECT i.id FROM content.item_text i WHERE i.batch_id = of_batch AND i.domain_id = of_domain AND i.emptied_at IS NULL ORDER BY i.id
$ids$;

REVOKE ALL ON FUNCTION public.batch_item_ids(bigint, integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.batch_item_ids(bigint, integer) TO app;

-- A batch changes in two ways: the draw of its sample (once, while open) and the closing (as the 0034 had it). Each needs its event in this transaction; the moments are the server's.
CREATE OR REPLACE FUNCTION public.refuse_batch_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
DECLARE
    sampling boolean := OLD.review_sample IS NULL AND NEW.review_sample IS NOT NULL;
BEGIN
    IF OLD.state IS DISTINCT FROM 'open' OR NEW.state IS NULL OR NEW.state NOT IN ('open', 'accepted', 'rejected')
        OR NEW.id IS DISTINCT FROM OLD.id OR NEW.domain_id IS DISTINCT FROM OLD.domain_id
        OR NEW.opened_by IS DISTINCT FROM OLD.opened_by OR NEW.opened_at IS DISTINCT FROM OLD.opened_at
        OR (OLD.review_sample IS NOT NULL AND NEW.review_sample IS DISTINCT FROM OLD.review_sample)
        OR NEW.sampled_at IS DISTINCT FROM OLD.sampled_at
        OR (sampling AND NEW.state <> 'open')
        OR (NEW.state = 'open' AND NOT sampling) THEN
        RAISE EXCEPTION 'a batch passes from open to accepted or to rejected, once, and its sample is drawn once; nothing else of it changes'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF (sampling AND NOT public.batch_event_of_this_transaction(NEW.id, 'batch_sampled', 'administrative_act', NULL))
        OR (NEW.state <> 'open' AND NOT public.batch_event_of_this_transaction(NEW.id, 'batch_closed', CASE NEW.state WHEN 'accepted' THEN 'batch_accepted' ELSE 'batch_rejected' END, NEW.motive)) THEN
        RAISE EXCEPTION 'a batch changes only with the event of the change, with the same outcome and motive, in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF sampling THEN
        NEW.sampled_at := pg_catalog.now();
    END IF;

    IF NEW.state <> 'open' THEN
        NEW.closed_at := pg_catalog.now();
    END IF;

    RETURN NEW;
END
$refuse$;

-- Content is added only to a batch that is open and not yet sampled (the 0035, with the draw closing the door): an item that came after the draw is one the sample could not have picked.
CREATE OR REPLACE FUNCTION public.refuse_content_outside_open_batch() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
DECLARE
    batch_state text;
    batch_sample text;
BEGIN
    NEW.written_in := pg_catalog.pg_current_xact_id();
    -- A row of a domain other than the declared one, or with none declared, is the row policy's to refuse, with its own refusal, and the batch is not looked for.
    IF NEW.domain_id = public.declared_domain() AND NOT public.restore_window_is_open() THEN
        PERFORM public.take_batch_turn(NEW.batch_id);
        SELECT b.state, b.review_sample INTO batch_state, batch_sample FROM content.ingestion_batch b WHERE b.id = NEW.batch_id AND b.domain_id = NEW.domain_id;
        IF batch_state IS DISTINCT FROM 'open' OR batch_sample IS NOT NULL THEN
            RAISE EXCEPTION 'content is added only to a batch that is open and whose sample was not drawn'
                USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
        END IF;
    END IF;

    RETURN NEW;
END
$refuse$;
