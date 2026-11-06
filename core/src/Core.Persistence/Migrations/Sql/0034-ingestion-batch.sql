-- The ingestion batch (issue 74): the unit in which real material comes in and goes out. A batch opens with no content, takes items while it is open, and closes once, accepted or rejected.
-- What the batch holds is invisible to every read of the stock until the batch is accepted, and a rejected batch loses its items in the very transaction that records the rejection, so that it
-- never leaves partial material reachable. Every guarantee is the database's: the application cannot forget the event, the state or the order.
--
-- The chain learns the batch: the item kind ingestion_batch (its id is the batch's own) and the steps batch_opened and batch_closed, with the reasons batch_accepted and batch_rejected.
-- The author of an opening is a person, who must be active; the author of a closing is a person or the system. The motive of a rejection is a name in snake_case, never free text, and it is
-- the reference of the event that closes the batch, so that the two records say the same thing. Only the checks change, from the last ones the earlier steps left (the item kinds from the 0023,
-- the steps from the 0031, the reasons from the 0022 and the shape of the reference from the 0030): everything they list and every clause they fix stays, and the batch comes in beside it.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_reason_listed,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN
        ('person', 'item_text', 'reversal_map', 'custody_chain', 'content_epoch', 'domain', 'sign_in_attempt', 'master_key', 'ingestion_batch')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed')),
    ADD CONSTRAINT custody_event_reason_listed CHECK (reason IN
        ('administrative_act', 'correction_of_record', 'content_read', 'verification_passed', 'verification_failed',
         'removal_requested_by_subject', 'contract_requirement', 'author_without_function', 'item_already_emptied',
         'credentials_accepted', 'password_wrong', 'account_not_recognized',
         'batch_accepted', 'batch_rejected')),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (reference IS NOT NULL)
            = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed',
                        'domain_label_revoked') OR reason = 'batch_rejected')
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL)),
    ADD CONSTRAINT custody_event_batch_shape CHECK (
        (step IN ('batch_opened', 'batch_closed')) = (item_kind = 'ingestion_batch')
        AND (step = 'batch_closed') = (reason IN ('batch_accepted', 'batch_rejected'))
        AND (step <> 'batch_opened' OR (reason = 'administrative_act' AND actor_person_id IS NOT NULL))
        AND (step NOT IN ('batch_opened', 'batch_closed') OR corrects IS NULL)
        AND (step <> 'batch_closed' OR (reason = 'batch_rejected') = (reference IS NOT NULL))
        AND (step <> 'batch_closed' OR reference IS NULL OR reference ~ '^[a-z][a-z0-9_]{0,62}$'));

-- Content that came in before the batches cannot be given one without inventing it, so the step refuses to apply while any row of content exists, in any domain, emptied or not.
DO $guard$
DECLARE
    declared integer;
BEGIN
    FOR declared IN SELECT id FROM public.domain LOOP
        PERFORM set_config('app.domain', declared::text, true);
        IF EXISTS (SELECT 1 FROM content.item_text) OR EXISTS (SELECT 1 FROM content.reversal_map) THEN
            RAISE EXCEPTION 'content exists that belongs to no batch: the step 0034 does not apply over it';
        END IF;
    END LOOP;

    PERFORM set_config('app.domain', '', true);
END
$guard$;

-- The batch: its state (open, accepted or rejected), the motive that only a rejection carries, who opened it and when, and when it closed. The domain is the axis of the table as of any content table,
-- and a batch is read by its domain whatever its state, so that a rejected one stays consultable with its motive after its material is gone. The moment of the closing is the server's.
SELECT public.create_content_table('ingestion_batch',
    $columns$state text NOT NULL DEFAULT 'open',
     motive text,
     opened_by bigint NOT NULL REFERENCES public.person (id),
     opened_at timestamptz NOT NULL DEFAULT now(),
     closed_at timestamptz,
     CONSTRAINT ingestion_batch_id_domain UNIQUE (id, domain_id),
     CONSTRAINT ingestion_batch_state CHECK (state IN ('open', 'accepted', 'rejected')),
     CONSTRAINT ingestion_batch_motive CHECK ((state = 'rejected') = (motive IS NOT NULL) AND (motive IS NULL OR motive ~ '^[a-z][a-z0-9_]{0,62}$')),
     CONSTRAINT ingestion_batch_closing CHECK ((state = 'open') = (closed_at IS NULL))$columns$);

-- Everyone reads the batches. The application names the domain and the person on opening and the state and the motive on closing, and nothing else: the state is born open, and the moments are the server's.
GRANT SELECT ON content.ingestion_batch TO PUBLIC;
GRANT INSERT (domain_id, opened_by) ON content.ingestion_batch TO app;
GRANT UPDATE (state, motive) ON content.ingestion_batch TO app;

-- Whether the window of a restore is open: the one time a row comes back in the state the copy had, which a batch closed or an item of a batch closed is not born in.
CREATE FUNCTION public.restore_window_is_open() RETURNS boolean
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog
RETURN EXISTS (SELECT 1 FROM public.restore_window w WHERE w.state = 'opened') AND NOT EXISTS (SELECT 1 FROM public.restore_window w WHERE w.state = 'closed');

-- Whether the event of the batch is in the chain and was written by this very transaction, as custody_event_of_this_transaction says it of an item (0018), and with the reason and the reference
-- the event carries: the reference is null for an opening and for an acceptance, and the motive for a rejection.
CREATE FUNCTION public.batch_event_of_this_transaction(of_batch bigint, of_step text, of_reason text, of_reference text) RETURNS boolean
LANGUAGE sql
STABLE
RETURN EXISTS (
    SELECT 1 FROM public.custody_event e
    WHERE e.item_kind = 'ingestion_batch' AND e.item_id = of_batch AND e.step = of_step AND e.reason = of_reason AND e.reference IS NOT DISTINCT FROM of_reference
      AND e.xmin = pg_catalog.pg_current_xact_id()::pg_catalog.xid
      AND e.occurred_at >= pg_catalog.transaction_timestamp());

-- A batch is born open, whoever inserts it, the owner included, except in the window of a restore.
CREATE FUNCTION public.refuse_batch_born_closed() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.state IS DISTINCT FROM 'open' AND NOT public.restore_window_is_open() THEN
        RAISE EXCEPTION 'a batch is born open'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

-- The event of the opening is looked for when the transaction ends, because the batch has no number until it is inserted and the event is about that number. It must be the opening of this batch, written
-- by the same transaction and by the person who opened it. The refusal carries the SQLSTATE of the other changes that need an event (CE005) and names the table and no value.
CREATE FUNCTION public.refuse_batch_without_open_event() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NOT public.restore_window_is_open() AND NOT EXISTS (
        SELECT 1 FROM public.custody_event e
        WHERE e.item_kind = 'ingestion_batch' AND e.item_id = NEW.id AND e.step = 'batch_opened' AND e.actor_person_id = NEW.opened_by
          AND e.xmin = pg_catalog.pg_current_xact_id()::pg_catalog.xid
          AND e.occurred_at >= pg_catalog.transaction_timestamp()) THEN
        RAISE EXCEPTION 'a batch is opened only with the event of its opening, by the same person, in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NULL;
END
$refuse$;

-- A batch closes once: from open to accepted or to rejected, and nothing else of it changes. The closing needs the event of the closing in this transaction, with the outcome as the reason and the motive
-- as the reference. The moment of the closing is the server's.
CREATE FUNCTION public.refuse_batch_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF OLD.state IS DISTINCT FROM 'open' OR NEW.state IS NULL OR NEW.state NOT IN ('accepted', 'rejected')
        OR NEW.id IS DISTINCT FROM OLD.id OR NEW.domain_id IS DISTINCT FROM OLD.domain_id
        OR NEW.opened_by IS DISTINCT FROM OLD.opened_by OR NEW.opened_at IS DISTINCT FROM OLD.opened_at THEN
        RAISE EXCEPTION 'a batch passes from open to accepted or to rejected, once, and nothing else of it changes'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF NOT public.batch_event_of_this_transaction(NEW.id, 'batch_closed', CASE NEW.state WHEN 'accepted' THEN 'batch_accepted' ELSE 'batch_rejected' END, NEW.motive) THEN
        RAISE EXCEPTION 'a batch is closed only with the event of its closing, with the same outcome and motive, in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    NEW.closed_at := pg_catalog.now();
    RETURN NEW;
END
$refuse$;

-- A batch is never removed, and the table is never emptied: the record of the attempt stays, rejected or not.
CREATE FUNCTION public.refuse_batch_removal() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'a batch is never removed: the record of the attempt stays'
        USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
END
$refuse$;

CREATE TRIGGER batch_is_born_open BEFORE INSERT ON content.ingestion_batch FOR EACH ROW EXECUTE FUNCTION public.refuse_batch_born_closed();
CREATE CONSTRAINT TRIGGER batch_open_needs_event AFTER INSERT ON content.ingestion_batch DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.refuse_batch_without_open_event();
CREATE TRIGGER batch_change_needs_event BEFORE UPDATE ON content.ingestion_batch FOR EACH ROW EXECUTE FUNCTION public.refuse_batch_change();
CREATE TRIGGER batch_is_never_removed BEFORE DELETE ON content.ingestion_batch FOR EACH ROW EXECUTE FUNCTION public.refuse_batch_removal();
CREATE TRIGGER batch_is_never_emptied BEFORE TRUNCATE ON content.ingestion_batch FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_batch_removal();

-- ALWAYS, so that the refusals also hold in a session that set session_replication_role to replica, which skips an ordinary trigger, as in the 0016 and the 0018.
ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_is_born_open;
ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_open_needs_event;
ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_change_needs_event;
ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_is_never_removed;
ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_is_never_emptied;

-- Every item and every map belongs to a batch of its own domain, by a key that carries the domain, as the map of an item does (0010), and a map belongs to the batch of its item. The transaction that wrote
-- the row is kept, as written_in, so that the one who writes sees what it wrote while the batch is open (an insert with RETURNING reads the row back through the policy below): the trigger fills it,
-- whatever the statement says.
ALTER TABLE content.item_text
    ADD COLUMN batch_id bigint NOT NULL,
    ADD COLUMN written_in xid8 NOT NULL DEFAULT pg_catalog.pg_current_xact_id(),
    ADD CONSTRAINT item_text_batch_fkey FOREIGN KEY (batch_id, domain_id) REFERENCES content.ingestion_batch (id, domain_id),
    ADD CONSTRAINT item_text_id_batch UNIQUE (id, batch_id);
ALTER TABLE content.reversal_map
    ADD COLUMN batch_id bigint NOT NULL,
    ADD COLUMN written_in xid8 NOT NULL DEFAULT pg_catalog.pg_current_xact_id(),
    ADD CONSTRAINT reversal_map_batch_fkey FOREIGN KEY (batch_id, domain_id) REFERENCES content.ingestion_batch (id, domain_id),
    ADD CONSTRAINT reversal_map_item_batch_fkey FOREIGN KEY (item_id, batch_id) REFERENCES content.item_text (id, batch_id);

-- Content is added only to a batch that is open (outside the window of a restore). The batch row is locked while the row goes in, so that a batch that closes meanwhile waits for it, or the row is refused
-- once the closing commits: no item enters a batch already accepted or rejected.
CREATE FUNCTION public.refuse_content_outside_open_batch() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
DECLARE
    batch_state text;
BEGIN
    NEW.written_in := pg_catalog.pg_current_xact_id();
    -- A row of a domain other than the declared one, or with none declared, is the row policy's to refuse, with its own refusal, and the batch is not looked for.
    IF NEW.domain_id = public.declared_domain() AND NOT public.restore_window_is_open() THEN
        SELECT b.state INTO batch_state FROM content.ingestion_batch b WHERE b.id = NEW.batch_id AND b.domain_id = NEW.domain_id FOR SHARE;
        IF batch_state IS DISTINCT FROM 'open' THEN
            RAISE EXCEPTION 'content is added only to a batch that is open'
                USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
        END IF;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER content_needs_open_batch BEFORE INSERT ON content.item_text FOR EACH ROW EXECUTE FUNCTION public.refuse_content_outside_open_batch();
CREATE TRIGGER content_needs_open_batch BEFORE INSERT ON content.reversal_map FOR EACH ROW EXECUTE FUNCTION public.refuse_content_outside_open_batch();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER content_needs_open_batch;
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER content_needs_open_batch;

-- A row of content is removed only with its batch rejected, and only in the transaction that wrote the event of the rejection: the batch is already rejected in the row, the event is the one of this batch with the
-- motive of the row. Nobody removes content any other way, the owner included.
CREATE FUNCTION public.refuse_content_removal_without_rejection() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM content.ingestion_batch b
        WHERE b.id = OLD.batch_id AND b.domain_id = OLD.domain_id AND b.state = 'rejected'
          AND public.batch_event_of_this_transaction(b.id, 'batch_closed', 'batch_rejected', b.motive)) THEN
        RAISE EXCEPTION 'content is removed only with its batch rejected, and with the event of the rejection in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN OLD;
END
$refuse$;

CREATE TRIGGER content_removal_needs_rejection BEFORE DELETE ON content.item_text FOR EACH ROW EXECUTE FUNCTION public.refuse_content_removal_without_rejection();
CREATE TRIGGER content_removal_needs_rejection BEFORE DELETE ON content.reversal_map FOR EACH ROW EXECUTE FUNCTION public.refuse_content_removal_without_rejection();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER content_removal_needs_rejection;
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER content_removal_needs_rejection;

-- The rejection removes the maps and then the items of the batch.
GRANT DELETE ON content.item_text, content.reversal_map TO app;

-- Visibility (issue 74, CA-5), by a restrictive row policy and not by a view: the policy applies to every read of the table, the ones the audit of the server counts and the ones of the rotation and of the
-- emptying included, and a read that names the table cannot choose a way around it. Without a view, the queries keep reading the table. The restrictive policy adds to the domain one, which stays as it was.
-- A row is seen when its batch is accepted; or, while the batch is open, by the transaction that wrote it; or by the transaction that wrote the event of the closing of its batch, which is the one that removes
-- the rows of a rejected batch. The review by sample of a batch still open (issue 83) reads it by a function of its own.
CREATE POLICY batch_accepted_only ON content.item_text AS RESTRICTIVE FOR SELECT
    USING (written_in = pg_catalog.pg_current_xact_id()
        OR EXISTS (SELECT 1 FROM content.ingestion_batch b WHERE b.id = item_text.batch_id AND b.domain_id = item_text.domain_id AND b.state = 'accepted')
        OR public.custody_event_of_this_transaction('ingestion_batch', batch_id, 'batch_closed'));
CREATE POLICY batch_accepted_only ON content.reversal_map AS RESTRICTIVE FOR SELECT
    USING (written_in = pg_catalog.pg_current_xact_id()
        OR EXISTS (SELECT 1 FROM content.ingestion_batch b WHERE b.id = reversal_map.batch_id AND b.domain_id = reversal_map.domain_id AND b.state = 'accepted')
        OR public.custody_event_of_this_transaction('ingestion_batch', batch_id, 'batch_closed'));

-- What the database counts of the content that is held (0025) is the content that can be read: it enters the count when its batch is accepted, and not when it is written, unless the batch is accepted
-- already when the row is written (a row put back by a restore). A rejected batch never enters it, and an item emptied leaves it as before.
DROP TRIGGER content_domain_is_counted ON content.item_text;
DROP TRIGGER content_domain_is_counted ON content.reversal_map;

CREATE FUNCTION public.count_content_of_accepted_batch_row() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $count$
BEGIN
    IF EXISTS (SELECT 1 FROM content.ingestion_batch b WHERE b.id = NEW.batch_id AND b.domain_id = NEW.domain_id AND b.state = 'accepted') THEN
        INSERT INTO public.content_domain (domain_id, table_name, delta) VALUES (NEW.domain_id, TG_TABLE_NAME, 1);
    END IF;

    RETURN NULL;
END
$count$;

CREATE TRIGGER content_domain_is_counted AFTER INSERT ON content.item_text FOR EACH ROW EXECUTE FUNCTION public.count_content_of_accepted_batch_row();
CREATE TRIGGER content_domain_is_counted AFTER INSERT ON content.reversal_map FOR EACH ROW EXECUTE FUNCTION public.count_content_of_accepted_batch_row();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER content_domain_is_counted;
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER content_domain_is_counted;

CREATE FUNCTION public.count_content_of_accepted_batch() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $count$
BEGIN
    INSERT INTO public.content_domain (domain_id, table_name, delta) SELECT NEW.domain_id, 'item_text', pg_catalog.count(*) FROM content.item_text i WHERE i.batch_id = NEW.id HAVING pg_catalog.count(*) > 0;
    INSERT INTO public.content_domain (domain_id, table_name, delta) SELECT NEW.domain_id, 'reversal_map', pg_catalog.count(*) FROM content.reversal_map m WHERE m.batch_id = NEW.id HAVING pg_catalog.count(*) > 0;
    RETURN NULL;
END
$count$;

CREATE TRIGGER content_of_batch_is_counted AFTER UPDATE ON content.ingestion_batch
    FOR EACH ROW WHEN (OLD.state = 'open' AND NEW.state = 'accepted') EXECUTE FUNCTION public.count_content_of_accepted_batch();
ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER content_of_batch_is_counted;

-- The load of a restore puts the batches back too (the 0032 as it was, with the table of the batches among the ones it accepts). The batches come before the items and the maps in the copy, and the
-- window of the restore is what lets a batch come back closed and a row come back into it.
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
                              'public.sign_in_attempt', 'content.ingestion_batch', 'content.item_text', 'content.reversal_map', 'content.item_emptied') THEN
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
