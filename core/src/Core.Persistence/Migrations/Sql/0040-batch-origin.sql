-- The origin of a submission (issue 75, decision 0025): who the material came from narrows who the people in it are, so the text is kept sealed as the raw text of an item is, under a key of its own
-- wrapped under the epoch, in a table of its own, and never read without an event of the chain. Only the clear state of it, declared or unknown, stays in the batch, which is what an answer may say.
-- The step origin_read is the reading of it, about the batch. The checks of the chain change from the last ones the earlier steps left (the step list from the 0036, the shape of the batch from the 0034):
-- every step and every clause they fix stays, and the new step comes in beside them.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_batch_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'origin_read')),
    ADD CONSTRAINT custody_event_batch_shape CHECK (
        (step IN ('batch_opened', 'batch_closed', 'origin_read')) = (item_kind = 'ingestion_batch')
        AND (step = 'batch_closed') = (reason IN ('batch_accepted', 'batch_rejected'))
        AND (step <> 'batch_opened' OR (reason = 'administrative_act' AND actor_person_id IS NOT NULL))
        AND (step <> 'origin_read' OR (reason = 'content_read' AND reference IS NULL))
        AND (step NOT IN ('batch_opened', 'batch_closed', 'origin_read') OR corrects IS NULL)
        AND (step <> 'batch_closed' OR (reason = 'batch_rejected') = (reference IS NOT NULL))
        AND (step <> 'batch_closed' OR reference IS NULL OR reference ~ '^[a-z][a-z0-9_]{0,62}$'));

-- Whether the submission declared its origin or chose to say it is unknown, in clear and born with the batch: a batch that a submission opens has it, and one that none opens has none.
ALTER TABLE content.ingestion_batch
    ADD COLUMN origin_state text,
    ADD CONSTRAINT ingestion_batch_origin_state CHECK ((submission IS NULL) = (origin_state IS NULL) AND (origin_state IS NULL OR origin_state IN ('declared', 'unknown')));

GRANT INSERT (origin_state) ON content.ingestion_batch TO app;

CREATE OR REPLACE FUNCTION public.refuse_batch_receipt_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.submission IS DISTINCT FROM OLD.submission OR NEW.received_bytes IS DISTINCT FROM OLD.received_bytes
        OR NEW.received_digest IS DISTINCT FROM OLD.received_digest OR NEW.warning IS DISTINCT FROM OLD.warning OR NEW.origin_state IS DISTINCT FROM OLD.origin_state THEN
        RAISE EXCEPTION 'what a submission brought never changes after the opening of its batch'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

-- The origin sealed: one row for each batch that declared it, with the key of the row wrapped as that of an item is (0017). The domain is the axis of the table as of any content table.
SELECT public.create_content_table('batch_origin',
    $columns$batch_id bigint NOT NULL,
     recorded_at timestamptz NOT NULL DEFAULT now(),
     origin bytea NOT NULL,
     dek_epoch integer NOT NULL,
     dek_wrapped bytea NOT NULL,
     CONSTRAINT batch_origin_once UNIQUE (batch_id),
     CONSTRAINT batch_origin_batch_fkey FOREIGN KEY (batch_id, domain_id) REFERENCES content.ingestion_batch (id, domain_id),
     CONSTRAINT batch_origin_key CHECK (dek_epoch > 0)$columns$);

COMMENT ON COLUMN content.batch_origin.origin IS 'protected:texto-bruto';
COMMENT ON COLUMN content.batch_origin.dek_wrapped IS 'protected:texto-bruto';

GRANT SELECT, INSERT ON content.batch_origin TO PUBLIC;
GRANT UPDATE (dek_epoch, dek_wrapped) ON content.batch_origin TO app;

-- The origin is final, and so is the row that holds it: only the key that wraps it changes, and only with the event of the rotation or of the rewrap in the same transaction, as for an item (0031).
CREATE FUNCTION public.refuse_origin_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.id IS DISTINCT FROM OLD.id OR NEW.domain_id IS DISTINCT FROM OLD.domain_id OR NEW.batch_id IS DISTINCT FROM OLD.batch_id
        OR NEW.recorded_at IS DISTINCT FROM OLD.recorded_at OR NEW.origin IS DISTINCT FROM OLD.origin THEN
        RAISE EXCEPTION 'the origin of a batch never changes: only the key that wraps it is wrapped again'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF NOT (public.custody_event_of_this_transaction('content_epoch', NEW.dek_epoch, 'epoch_rotated')
        OR (NEW.dek_epoch = OLD.dek_epoch AND public.custody_event_of_this_transaction('domain', NEW.domain_id, 'domain_rewrapped'))) THEN
        RAISE EXCEPTION 'a key is wrapped again only with the event of the rotation or of the rewrap in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE FUNCTION public.refuse_origin_removal() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'the origin of a batch is never removed'
        USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
END
$refuse$;

CREATE TRIGGER origin_change_is_bounded BEFORE UPDATE ON content.batch_origin FOR EACH ROW EXECUTE FUNCTION public.refuse_origin_change();
CREATE TRIGGER origin_is_never_removed BEFORE DELETE ON content.batch_origin FOR EACH ROW EXECUTE FUNCTION public.refuse_origin_removal();
CREATE TRIGGER origin_is_never_emptied BEFORE TRUNCATE ON content.batch_origin FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_origin_removal();
ALTER TABLE content.batch_origin ENABLE ALWAYS TRIGGER origin_change_is_bounded;
ALTER TABLE content.batch_origin ENABLE ALWAYS TRIGGER origin_is_never_removed;
ALTER TABLE content.batch_origin ENABLE ALWAYS TRIGGER origin_is_never_emptied;

CREATE OR REPLACE FUNCTION public.restore_load(table_name text, rows jsonb) RETURNS bigint LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $load$
DECLARE
    target regclass;
    declared integer;
    part bigint;
    loaded bigint := 0;
BEGIN
    PERFORM public.restore_require_open();
    BEGIN
        IF table_name NOT IN ('public.domain', 'public.derivation_label', 'public.derivation_label_revocation', 'public.person', 'public.person_change', 'public.person_credential',
                              'public.sign_in_attempt', 'content.ingestion_batch', 'content.batch_origin', 'content.item_text', 'content.reversal_map', 'content.item_emptied') THEN
            RAISE EXCEPTION 'the table is not one the load puts back' USING ERRCODE = 'CE005';
        END IF;

        target := table_name::regclass;
        IF table_name LIKE 'content.%' THEN
            FOR declared IN SELECT DISTINCT (r ->> 'domain_id')::integer FROM jsonb_array_elements(rows) r LOOP
                PERFORM pg_catalog.set_config('app.domain', declared::text, true);
                EXECUTE format('INSERT INTO %s OVERRIDING SYSTEM VALUE SELECT * FROM jsonb_populate_recordset(NULL::%s, $1) WHERE domain_id = $2', target, target) USING rows, declared;
                GET DIAGNOSTICS part = ROW_COUNT;
                loaded := loaded + part;
            END LOOP;
        ELSE
            EXECUTE format('INSERT INTO %s OVERRIDING SYSTEM VALUE SELECT * FROM jsonb_populate_recordset(NULL::%s, $1)', target, target) USING rows;
            GET DIAGNOSTICS loaded = ROW_COUNT;
        END IF;

        IF table_name = 'public.person' THEN
            INSERT INTO public.restore_inactive SELECT id FROM public.person WHERE NOT active;
            UPDATE public.person SET active = true WHERE NOT active;
        END IF;

        IF table_name NOT IN ('public.derivation_label', 'public.derivation_label_revocation', 'public.person_credential') AND loaded > 0 THEN
            PERFORM pg_catalog.setval(pg_catalog.pg_get_serial_sequence(table_name, 'id')::regclass, (SELECT max((r ->> 'id')::bigint) FROM jsonb_array_elements(rows) r));
        END IF;
    EXCEPTION WHEN OTHERS THEN
        PERFORM public.restore_close(false);
        RETURN -1;
    END;

    RETURN loaded;
END
$load$;

-- The link of the step 0022 as it was, with origin_read among the readings that a chain that failed its last verification still takes, as the other readings of content are (0013): a reading is a defect to stop, not to hide.
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

    IF NEW.step NOT IN ('verify_chain', 'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'origin_read', 'sign_in_accepted', 'sign_in_refused', 'credential_recomputed')
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
