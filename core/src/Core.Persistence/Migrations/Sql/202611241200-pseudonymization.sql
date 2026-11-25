-- The pseudonymization of an item (issue 101, fase 5 fatia B): the pseudonymized text is sealed with the key of the item, as decision 0030 asks; the map of the item goes with it in the
-- same transaction; and the markers it holds go in clear, since none keeps anything of the original nor anything drawn from it (0003).

-- The markers held in the installation: one row for each marker drawn, with its domain, under the row policy. The unique marker makes a draw that another domain already holds lose
-- in the index, without reading the other domain, and the marker of an item emptied, rejected or gone from its text stays here, so that it never comes back in another conversation.
SELECT public.create_content_table('marker_in_use',
    $columns$marker text NOT NULL,
     kind text NOT NULL,
     CONSTRAINT marker_in_use_once UNIQUE (marker),
     CONSTRAINT marker_in_use_marker_domain UNIQUE (marker, domain_id),
     CONSTRAINT marker_in_use_form CHECK ((kind = 'person' AND marker ~ '^PESSOA_[0-9]{8}$') OR (kind = 'place' AND marker ~ '^LUGAR_[0-9]{8}$')
         OR (kind = 'organization' AND marker ~ '^ORGANIZACAO_[0-9]{8}$'))$columns$);

-- The markers an item holds, whether each stands for an author, the source of the counts of the batch; emptied with the item and removed with its rejected batch.
SELECT public.create_content_table('item_marker',
    $columns$item_id bigint NOT NULL,
     marker text NOT NULL,
     author boolean NOT NULL,
     CONSTRAINT item_marker_item FOREIGN KEY (item_id, domain_id) REFERENCES content.item_text (id, domain_id),
     CONSTRAINT item_marker_in_use FOREIGN KEY (marker, domain_id) REFERENCES content.marker_in_use (marker, domain_id),
     CONSTRAINT item_marker_once UNIQUE (item_id, marker),
     CONSTRAINT item_marker_one_item UNIQUE (marker)$columns$);

GRANT SELECT, INSERT ON content.marker_in_use, content.item_marker TO PUBLIC;
GRANT DELETE ON content.item_marker TO app;

-- A marker drawn is never released: no row of the markers in use changes or goes.
CREATE FUNCTION public.refuse_marker_release() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'a marker drawn stays held: releasing it would let it come back in another conversation'
        USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
END
$refuse$;

CREATE TRIGGER marker_is_never_released BEFORE UPDATE OR DELETE ON content.marker_in_use FOR EACH ROW EXECUTE FUNCTION public.refuse_marker_release();
CREATE TRIGGER markers_are_never_emptied BEFORE TRUNCATE ON content.marker_in_use FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_marker_release();
ALTER TABLE content.marker_in_use ENABLE ALWAYS TRIGGER marker_is_never_released;
ALTER TABLE content.marker_in_use ENABLE ALWAYS TRIGGER markers_are_never_emptied;

-- The markers of an item are written with the item or with its rerun: the item carries this transaction as xmin, or the event of its rerun is in it. A marker new to the item is one
-- this transaction won in the markers held, so that none another item holds, or held before it was emptied or rejected, is ever given to it (0057). They go only with the event of
-- the emptying or of the rerun of the item, or with its batch rejected and the event of the rejection, in the same transaction.
CREATE FUNCTION public.refuse_item_marker_outside_its_path() RETURNS trigger
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

    IF TG_OP = 'INSERT' AND NOT (EXISTS (SELECT 1 FROM content.item_marker m WHERE m.item_id = NEW.item_id AND m.marker = NEW.marker)
        OR EXISTS (SELECT 1 FROM content.marker_in_use u WHERE u.marker = NEW.marker AND u.domain_id = NEW.domain_id AND u.xmin = pg_catalog.pg_current_xact_id()::pg_catalog.xid)) THEN
        RAISE EXCEPTION 'a marker new to an item is one won in the same transaction'
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

CREATE TRIGGER item_marker_is_never_changed BEFORE UPDATE ON content.item_marker FOR EACH ROW EXECUTE FUNCTION public.refuse_marker_release();
CREATE TRIGGER item_marker_follows_its_item BEFORE INSERT OR DELETE ON content.item_marker FOR EACH ROW EXECUTE FUNCTION public.refuse_item_marker_outside_its_path();
CREATE TRIGGER item_markers_are_never_emptied BEFORE TRUNCATE ON content.item_marker FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_marker_release();
ALTER TABLE content.item_marker ENABLE ALWAYS TRIGGER item_marker_is_never_changed;
ALTER TABLE content.item_marker ENABLE ALWAYS TRIGGER item_marker_follows_its_item;
ALTER TABLE content.item_marker ENABLE ALWAYS TRIGGER item_markers_are_never_emptied;

-- The pseudonymized text is sealed (decision 0030, line 24): no item holds it yet, and a text in clear is never turned into the sealed form here. The trigger that names the column is
-- made again around the change of type, as it was. The row policy is lifted for the owner only while the migration looks at every domain, in this transaction.
ALTER TABLE content.item_text NO FORCE ROW LEVEL SECURITY;
DO $empty$
BEGIN
    IF EXISTS (SELECT 1 FROM content.item_text WHERE pseudonymized_text IS NOT NULL) THEN
        RAISE EXCEPTION 'an item holds a pseudonymized text in clear: it is not sealed by a migration';
    END IF;
END
$empty$;
ALTER TABLE content.item_text FORCE ROW LEVEL SECURITY;

DROP TRIGGER emptied_content_is_final ON content.item_text;
ALTER TABLE content.item_text ALTER COLUMN pseudonymized_text TYPE bytea USING NULL;
COMMENT ON COLUMN content.item_text.pseudonymized_text IS 'protected:texto-bruto';
CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL
        OR (NEW.emptied_at IS NULL
            AND (NEW.raw_text IS DISTINCT FROM OLD.raw_text OR NEW.pseudonymized_text IS DISTINCT FROM OLD.pseudonymized_text
                OR NEW.occurrences IS DISTINCT FROM OLD.occurrences
                OR NEW.removed_passages IS DISTINCT FROM OLD.removed_passages OR NEW.pseudonymized_passages IS DISTINCT FROM OLD.pseudonymized_passages
                OR NEW.preserved_passages IS DISTINCT FROM OLD.preserved_passages)))
    EXECUTE FUNCTION public.refuse_change_of_emptied_content();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER emptied_content_is_final;

-- The database cannot read the sealed text, so it holds the pseudonymized one to its path: an item holds it only with a map of its own written in the same transaction, checked at the
-- commit so that the map may follow the item; and a batch holding it is accepted only with the event batch_pseudonymized in the transaction of the acceptance.
CREATE FUNCTION public.refuse_pseudonymized_without_map() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NOT public.restore_window_is_open() AND NOT EXISTS (
        SELECT 1 FROM content.reversal_map m
        WHERE m.item_id = NEW.id AND m.domain_id = NEW.domain_id AND m.emptied_at IS NULL AND m.xmin = pg_catalog.pg_current_xact_id()::pg_catalog.xid) THEN
        RAISE EXCEPTION 'an item holds a pseudonymized text only with a map of its own written in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NULL;
END
$refuse$;

CREATE CONSTRAINT TRIGGER pseudonymized_needs_map AFTER INSERT OR UPDATE OF pseudonymized_text ON content.item_text DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW WHEN (NEW.pseudonymized_text IS NOT NULL) EXECUTE FUNCTION public.refuse_pseudonymized_without_map();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER pseudonymized_needs_map;

-- Whether some item of the batch holds a pseudonymized text. The batch is still open when it is accepted, and the row policy shows its items only to the transaction that wrote them, so
-- the question reads in the scope of the system, as the counts of migration 0057 do; the trigger of the acceptance and the batches of production ask the same function, so the two never
-- disagree, and the application never filters by the sealed column itself.
CREATE FUNCTION public.batch_holds_pseudonymized(of_batch bigint, of_domain integer) RETURNS boolean
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS $holds$
DECLARE
    previous text := pg_catalog.current_setting('app.content_scope', true);
    holds boolean;
BEGIN
    PERFORM pg_catalog.set_config('app.content_scope', 'system', true);
    holds := EXISTS (SELECT 1 FROM content.item_text i WHERE i.batch_id = of_batch AND i.domain_id = of_domain AND i.pseudonymized_text IS NOT NULL);
    PERFORM pg_catalog.set_config('app.content_scope', COALESCE(previous, ''), true);
    RETURN holds;
END
$holds$;

REVOKE ALL ON FUNCTION public.batch_holds_pseudonymized(bigint, integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.batch_holds_pseudonymized(bigint, integer) TO app;

CREATE FUNCTION public.refuse_pseudonymized_batch_without_event() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF public.batch_holds_pseudonymized(NEW.id, NEW.domain_id)
        AND NOT public.custody_event_of_this_transaction('ingestion_batch', NEW.id, 'batch_pseudonymized') THEN
        RAISE EXCEPTION 'a batch holding pseudonymized items is accepted only with the event of the pseudonymization in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER pseudonymized_batch_needs_event BEFORE UPDATE OF state ON content.ingestion_batch
    FOR EACH ROW WHEN (OLD.state = 'open' AND NEW.state = 'accepted') EXECUTE FUNCTION public.refuse_pseudonymized_batch_without_event();
ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER pseudonymized_batch_needs_event;

-- The chain learns the step batch_pseudonymized, one for each batch accepted with pseudonymized items, in the transaction of the acceptance: the item is the batch, with no reference
-- and no correction. The step list is the one of the sanitization-rerun with the new name; the shape of a batch is the one of the batch-sanitized-event with the new step beside the others.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_batch_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'origin_read', 'configuration_approved', 'configuration_refused',
         'configuration_in_force', 'person_email_corrected', 'configuration_not_applied', 'occurrences_read', 'batch_sampled', 'batch_reviewed', 'batch_sanitized',
         'sanitization_rerun', 'item_resanitized', 'batch_pseudonymized')),
    ADD CONSTRAINT custody_event_batch_shape CHECK (
        (step IN ('batch_opened', 'batch_closed', 'batch_sampled', 'batch_reviewed', 'batch_sanitized', 'batch_pseudonymized', 'origin_read')) = (item_kind = 'ingestion_batch')
        AND (step = 'batch_closed') = (reason IN ('batch_accepted', 'batch_rejected'))
        AND (step = 'batch_reviewed') = (reason IN ('review_clean', 'review_found'))
        AND (step NOT IN ('batch_opened', 'batch_sampled') OR (reason = 'administrative_act' AND actor_person_id IS NOT NULL))
        AND (step <> 'batch_reviewed' OR actor_person_id IS NOT NULL)
        AND (step <> 'origin_read' OR (reason = 'content_read' AND reference IS NULL))
        AND (step NOT IN ('batch_opened', 'batch_closed', 'batch_sampled', 'batch_reviewed', 'batch_sanitized', 'batch_pseudonymized', 'origin_read') OR corrects IS NULL)
        AND (step <> 'batch_sanitized' OR (reason = 'administrative_act' AND reference ~ '^[0-9a-f]{64}$'))
        AND (step <> 'batch_pseudonymized' OR (reason = 'administrative_act' AND reference IS NULL))
        AND (step <> 'batch_closed' OR (reason = 'batch_rejected') = (reference IS NOT NULL))
        AND (step <> 'batch_closed' OR reference IS NULL OR reference ~ '^[a-z][a-z0-9_]{0,62}$'));

-- The numbers of the pseudonymization of a batch for its detail: how many markers of each kind its items hold, the authors apart, and how many items hold one, never a marker nor a
-- word. Like the counts of migration 0057, it reads in the scope of the system and the row policy judges the domain, so a batch of another domain counts nothing.
CREATE FUNCTION public.batch_marker_counts(of_batch bigint, of_domain integer) RETURNS TABLE (count_kind text, count_markers bigint, count_items bigint)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS $counts$
DECLARE
    previous text := pg_catalog.current_setting('app.content_scope', true);
BEGIN
    PERFORM pg_catalog.set_config('app.content_scope', 'system', true);
    RETURN QUERY
        SELECT CASE WHEN m.author THEN 'author' ELSE u.kind END, pg_catalog.count(DISTINCT m.marker), pg_catalog.count(DISTINCT m.item_id)
        FROM content.item_marker m
        JOIN content.marker_in_use u ON u.marker = m.marker AND u.domain_id = m.domain_id
        JOIN content.item_text i ON i.id = m.item_id AND i.domain_id = m.domain_id
        WHERE i.batch_id = of_batch AND i.domain_id = of_domain AND i.emptied_at IS NULL
        GROUP BY 1;
    PERFORM pg_catalog.set_config('app.content_scope', COALESCE(previous, ''), true);
END
$counts$;

REVOKE ALL ON FUNCTION public.batch_marker_counts(bigint, integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.batch_marker_counts(bigint, integer) TO app;
