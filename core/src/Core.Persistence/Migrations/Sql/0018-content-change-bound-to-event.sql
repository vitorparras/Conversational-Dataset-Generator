-- The database ties a change of protected content to an event of the chain written in the same transaction (decision 0049). Until this step the application emptied an item, or wrapped its key
-- again, and the chain was told by the application and by nothing else; a defect there would lose content without a trace. Now the change is refused unless the event is already in the chain.
--
-- The rotation of the epoch is an event too: the step epoch_rotated, about the epoch the keys are wrapped under (item kind content_epoch, its id is the number of the epoch), written by the system.
-- One event covers every key the rotation wraps again, in the one transaction in which it does.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN ('person', 'item_text', 'reversal_map', 'custody_chain', 'content_epoch')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated')),
    ADD CONSTRAINT custody_event_rotation_shape CHECK (
        (step = 'epoch_rotated') = (item_kind = 'content_epoch')
        AND (step <> 'epoch_rotated' OR (actor_system AND reason = 'administrative_act' AND reference IS NULL)));

-- The trigger looks for the event once per row it lets through, and the rotation changes every key: without an index of its own the chain is read from end to end for each key wrapped again, and
-- the event of the rotation holds the tip of the chain (previous_digest is unique) until the commit, so every other writer of an event waits for as long as that takes.
CREATE INDEX custody_event_item_step_idx ON public.custody_event (item_kind, item_id, step);

-- Whether the event is in the chain and was written by this very transaction. The row carries the transaction that wrote it (xmin), and the server's own is compared with it: an event that
-- another transaction wrote, committed or not, never matches, so no concurrent writer can release a change that is not its own. An event written inside a savepoint carries the number of
-- the savepoint and does not match either; the refusal fails closed.
-- The transaction number (xmin) is 32 bits and a frozen row keeps the raw one, so a turn of the counter could make an old event of the same item, kind and step match; an event written in this
-- transaction is never older than its start, and an older one does not match. A clock set back between the start and the event fails closed, with the same refusal, and repeating clears it.
CREATE FUNCTION public.custody_event_of_this_transaction(of_kind text, of_item bigint, of_step text) RETURNS boolean
LANGUAGE sql
STABLE
RETURN EXISTS (
    SELECT 1 FROM public.custody_event e
    WHERE e.item_kind = of_kind AND e.item_id = of_item AND e.step = of_step AND e.xmin = pg_catalog.pg_current_xact_id()::pg_catalog.xid
      AND e.occurred_at >= pg_catalog.transaction_timestamp());

-- The refusal carries an SQLSTATE of its own (CE005) and a message that names the table and no value. It holds for every role, the owner included, and in a session that set
-- session_replication_role to replica, as the triggers of the step 0016 do.
CREATE FUNCTION public.refuse_item_change_without_event() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.emptied_at IS NOT NULL THEN
        IF NOT public.custody_event_of_this_transaction('item_text', NEW.id, 'item_emptied') THEN
            RAISE EXCEPTION 'an item is emptied only with the event of its emptying in the same transaction'
                USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
        END IF;
    ELSIF NOT public.custody_event_of_this_transaction('content_epoch', NEW.dek_epoch, 'epoch_rotated') THEN
        RAISE EXCEPTION 'a key is wrapped again only with the event of the rotation in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

-- A map is emptied with its item: the event is the one of the item it belongs to.
CREATE FUNCTION public.refuse_map_change_without_event() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.emptied_at IS NOT NULL THEN
        IF NOT public.custody_event_of_this_transaction('item_text', NEW.item_id, 'item_emptied') THEN
            RAISE EXCEPTION 'a map is emptied only with the event of the emptying of its item in the same transaction'
                USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
        END IF;
    ELSIF NOT public.custody_event_of_this_transaction('content_epoch', NEW.dek_epoch, 'epoch_rotated') THEN
        RAISE EXCEPTION 'a key is wrapped again only with the event of the rotation in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

-- Only a row not emptied is looked at: the one emptied is final already (CE004). The change that counts is the marker set, or the wrapped key or its epoch changed.
CREATE TRIGGER item_change_needs_event BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NULL
        AND (NEW.emptied_at IS NOT NULL OR NEW.dek_wrapped IS DISTINCT FROM OLD.dek_wrapped OR NEW.dek_epoch IS DISTINCT FROM OLD.dek_epoch))
    EXECUTE FUNCTION public.refuse_item_change_without_event();
CREATE TRIGGER map_change_needs_event BEFORE UPDATE ON content.reversal_map
    FOR EACH ROW WHEN (OLD.emptied_at IS NULL
        AND (NEW.emptied_at IS NOT NULL OR NEW.dek_wrapped IS DISTINCT FROM OLD.dek_wrapped OR NEW.dek_epoch IS DISTINCT FROM OLD.dek_epoch))
    EXECUTE FUNCTION public.refuse_map_change_without_event();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER item_change_needs_event;
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER map_change_needs_event;
