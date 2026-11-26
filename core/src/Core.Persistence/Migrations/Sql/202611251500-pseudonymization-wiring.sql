-- The pseudonymization wired to the submission and to the rerun (issue 102, fase 5 fatia F, F3). The marker belongs to the conversation, held by the batch it came in with
-- (decision 0057, amended): the messages of one conversation share it, and a marker new to an item comes from its own batch, never from another.

-- The markers held learn their batch. No item holds a marker before this slice, so no row is given a batch by a migration; the row policy is lifted for the owner only while the
-- migration looks at every domain, in this transaction.
ALTER TABLE content.marker_in_use NO FORCE ROW LEVEL SECURITY;
DO $empty$
BEGIN
    IF EXISTS (SELECT 1 FROM content.marker_in_use) THEN
        RAISE EXCEPTION 'a marker is held before the markers know their batch: no batch is given to it by a migration';
    END IF;
END
$empty$;
ALTER TABLE content.marker_in_use FORCE ROW LEVEL SECURITY;

ALTER TABLE content.marker_in_use
    ADD COLUMN batch_id bigint NOT NULL,
    ADD CONSTRAINT marker_in_use_batch FOREIGN KEY (batch_id, domain_id) REFERENCES content.ingestion_batch (id, domain_id);

-- Every message of the conversation holds the markers it shares: the marker is unique for each item only. A batch that is no conversation keeps it unique for each item, below.
ALTER TABLE content.item_marker DROP CONSTRAINT item_marker_one_item;

-- The markers of an item are written with the item or with its rerun, as before; a marker new to the item is one its batch holds, won in this transaction or by an earlier message of
-- the same conversation, so that none another conversation holds, or held before it was emptied or rejected, is ever given to it (0057). In a batch that is no conversation each item
-- counts as a conversation of its own, so no other item of it holds the marker; the batch has no form declared only before the form was recorded, and counts as none.
CREATE OR REPLACE FUNCTION public.refuse_item_marker_outside_its_path() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    -- A row of a domain other than the declared one, or with none declared (a row of no domain included), is the row policy's to refuse, with its own refusal.
    IF TG_OP = 'INSERT' AND (public.declared_domain() IS NULL OR NEW.domain_id IS DISTINCT FROM public.declared_domain()) THEN
        RETURN NEW;
    END IF;

    IF TG_OP = 'INSERT' AND NOT (public.custody_event_of_this_transaction('item_text', NEW.item_id, 'item_resanitized')
        OR EXISTS (SELECT 1 FROM content.item_text i WHERE i.id = NEW.item_id AND i.domain_id = NEW.domain_id AND i.xmin = pg_catalog.pg_current_xact_id()::pg_catalog.xid)) THEN
        RAISE EXCEPTION 'the markers of an item are written with the item or with the event of its rerun, in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF TG_OP = 'INSERT' AND NOT EXISTS (
        SELECT 1 FROM content.marker_in_use u JOIN content.item_text i ON i.batch_id = u.batch_id AND i.domain_id = u.domain_id
        WHERE u.marker = NEW.marker AND u.domain_id = NEW.domain_id AND i.id = NEW.item_id) THEN
        RAISE EXCEPTION 'a marker new to an item is one its batch holds'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF TG_OP = 'INSERT' AND EXISTS (SELECT 1 FROM content.item_marker m WHERE m.marker = NEW.marker AND m.domain_id = NEW.domain_id AND m.item_id <> NEW.item_id)
        AND NOT EXISTS (SELECT 1 FROM content.item_text i JOIN content.ingestion_batch b ON b.id = i.batch_id AND b.domain_id = i.domain_id
                        WHERE i.id = NEW.item_id AND i.domain_id = NEW.domain_id AND b.declared_form = 'conversation') THEN
        RAISE EXCEPTION 'a marker of an item outside a conversation is held by no other item'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF TG_OP = 'DELETE' AND NOT (public.custody_event_of_this_transaction('item_text', OLD.item_id, 'item_emptied')
        OR public.custody_event_of_this_transaction('item_text', OLD.item_id, 'item_resanitized')
        OR EXISTS (SELECT 1 FROM content.item_text i JOIN content.ingestion_batch b ON b.id = i.batch_id AND b.domain_id = i.domain_id
                   WHERE i.id = OLD.item_id AND i.domain_id = OLD.domain_id AND b.state = 'rejected'
                     AND public.batch_event_of_this_transaction(b.id, 'batch_closed', 'batch_rejected', b.motive))) THEN
        RAISE EXCEPTION 'the markers of an item go only with the event of its emptying or of its rerun, or with its batch rejected, in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;

    RETURN NEW;
END
$refuse$;

-- The pseudonymized text changes with the rerun of the item: it leaves the trigger of the texts a row keeps and joins the one that lets the sanitization change only with a rerun.
DROP TRIGGER emptied_content_is_final ON content.item_text;
CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL
        OR (NEW.emptied_at IS NULL
            AND (NEW.raw_text IS DISTINCT FROM OLD.raw_text OR NEW.occurrences IS DISTINCT FROM OLD.occurrences
                OR NEW.removed_passages IS DISTINCT FROM OLD.removed_passages OR NEW.pseudonymized_passages IS DISTINCT FROM OLD.pseudonymized_passages
                OR NEW.preserved_passages IS DISTINCT FROM OLD.preserved_passages)))
    EXECUTE FUNCTION public.refuse_change_of_emptied_content();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER emptied_content_is_final;

DROP TRIGGER resanitization_needs_rerun ON content.item_text;
CREATE TRIGGER resanitization_needs_rerun BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NULL AND NEW.emptied_at IS NULL
        AND (NEW.sanitized_text IS DISTINCT FROM OLD.sanitized_text OR NEW.sanitization_version IS DISTINCT FROM OLD.sanitization_version
            OR NEW.sanitization_trail IS DISTINCT FROM OLD.sanitization_trail OR NEW.sanitization_counts IS DISTINCT FROM OLD.sanitization_counts
            OR NEW.sanitization_untreated IS DISTINCT FROM OLD.sanitization_untreated OR NEW.pseudonymized_text IS DISTINCT FROM OLD.pseudonymized_text))
    EXECUTE FUNCTION public.refuse_resanitization_without_rerun();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER resanitization_needs_rerun;

-- A pseudonymized item whose sanitized text changes is pseudonymized again in the same update: the text it shows is never the swap of a sanitization it no longer holds.
CREATE FUNCTION public.refuse_resanitization_without_pseudonymized() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'a pseudonymized item changes its sanitized text only with a pseudonymized text of its own'
        USING ERRCODE = 'CE004', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
END
$refuse$;

CREATE TRIGGER resanitized_needs_pseudonymized BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NULL AND NEW.emptied_at IS NULL AND OLD.pseudonymized_text IS NOT NULL
        AND NEW.sanitized_text IS DISTINCT FROM OLD.sanitized_text AND NEW.pseudonymized_text IS NOT DISTINCT FROM OLD.pseudonymized_text)
    EXECUTE FUNCTION public.refuse_resanitization_without_pseudonymized();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER resanitized_needs_pseudonymized;

-- An item holds one map not emptied: the rerun empties the old one before it writes the new.
CREATE UNIQUE INDEX reversal_map_one_live ON content.reversal_map (item_id) WHERE emptied_at IS NULL;

-- The rerun empties the old map with the event of the rerun of its item, as the emptying does with its own (CE004 and CE005 otherwise).
CREATE OR REPLACE FUNCTION public.refuse_map_emptied_before_item() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NOT (EXISTS (SELECT 1 FROM content.item_emptied e WHERE e.item_id = NEW.item_id AND e.domain_id = NEW.domain_id)
        OR public.custody_event_of_this_transaction('item_text', NEW.item_id, 'item_resanitized')) THEN
        RAISE EXCEPTION 'a map is emptied with its item, not before'
            USING ERRCODE = 'CE004', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE OR REPLACE FUNCTION public.refuse_map_change_without_event() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.emptied_at IS NOT NULL THEN
        IF NOT (public.custody_event_of_this_transaction('item_text', NEW.item_id, 'item_emptied')
            OR public.custody_event_of_this_transaction('item_text', NEW.item_id, 'item_resanitized')) THEN
            RAISE EXCEPTION 'a map is emptied only with the event of the emptying or of the rerun of its item in the same transaction'
                USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
        END IF;
    ELSIF NOT (public.custody_event_of_this_transaction('content_epoch', NEW.dek_epoch, 'epoch_rotated')
        OR (NEW.dek_epoch = OLD.dek_epoch AND public.custody_event_of_this_transaction('domain', NEW.domain_id, 'domain_rewrapped'))) THEN
        RAISE EXCEPTION 'a key is wrapped again only with the event of the rotation or of the rewrap in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;
    RETURN NEW;
END
$refuse$;

-- The new map of an item rerun goes to its batch, accepted already, with the event of the rerun of the item; any other content still needs a batch open and with no sample (0053).
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
        -- The item of the row is read in its own statement: PL/pgSQL plans an expression whole, and the item text has no item_id to plan it with.
        IF TG_TABLE_NAME = 'reversal_map' THEN
            IF public.custody_event_of_this_transaction('item_text', NEW.item_id, 'item_resanitized') THEN
                RETURN NEW;
            END IF;
        END IF;

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
