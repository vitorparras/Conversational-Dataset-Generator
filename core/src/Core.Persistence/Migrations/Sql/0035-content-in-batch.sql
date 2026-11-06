-- The content comes into the batch (issue 74, second step, after the 0034, which made the batch): every item and every map belongs to a batch, the content of a batch is invisible to every read of the stock until the
-- batch is accepted, and a rejected batch loses its items in the very transaction that records the rejection, so that it never leaves partial material reachable. Every guarantee is the database's.

-- Content that came in before the batches cannot be given one without inventing it, so the step refuses to apply while any row of content exists, in any domain, emptied or not.
DO $guard$
DECLARE
    declared integer;
BEGIN
    FOR declared IN SELECT id FROM public.domain LOOP
        PERFORM set_config('app.domain', declared::text, true);
        IF EXISTS (SELECT 1 FROM content.item_text) OR EXISTS (SELECT 1 FROM content.reversal_map) THEN
            RAISE EXCEPTION 'content exists that belongs to no batch: the step 0035 does not apply over it';
        END IF;
    END LOOP;

    PERFORM set_config('app.domain', '', true);
END
$guard$;

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

-- Visibility (issue 74, CA-5), by a restrictive row policy and not by a view: the policy applies to every read of the table, the ones the audit of the server counts included, and a read that names the table
-- cannot choose a way around it. Without a view, the queries keep reading the table. The restrictive policy adds to the domain one, which stays as it was.
-- A row is seen when its batch is accepted; or, while the batch is open, by the transaction that wrote it; or by the transaction that wrote the event of the closing of its batch, which is the one that removes
-- the rows of a rejected batch; or in the scope of the system (app.content_scope = system), the one of the operations that only wrap a key again (the rotation of the epoch and the rewrap of a domain), that
-- check the epochs in use, and that copy the data, which must carry every item and every map, so that a batch accepted later is found with its keys on the epoch that is held and is restored whole. The scope
-- is declared by those paths and by none that reads the stock. The review by sample of a batch still open (issue 83) reads it by a function of its own.
CREATE POLICY batch_accepted_only ON content.item_text AS RESTRICTIVE FOR SELECT
    USING (written_in = pg_catalog.pg_current_xact_id()
        OR EXISTS (SELECT 1 FROM content.ingestion_batch b WHERE b.id = item_text.batch_id AND b.domain_id = item_text.domain_id AND b.state = 'accepted')
        OR public.custody_event_of_this_transaction('ingestion_batch', batch_id, 'batch_closed')
        OR pg_catalog.current_setting('app.content_scope', true) = 'system');
CREATE POLICY batch_accepted_only ON content.reversal_map AS RESTRICTIVE FOR SELECT
    USING (written_in = pg_catalog.pg_current_xact_id()
        OR EXISTS (SELECT 1 FROM content.ingestion_batch b WHERE b.id = reversal_map.batch_id AND b.domain_id = reversal_map.domain_id AND b.state = 'accepted')
        OR public.custody_event_of_this_transaction('ingestion_batch', batch_id, 'batch_closed')
        OR pg_catalog.current_setting('app.content_scope', true) = 'system');

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
