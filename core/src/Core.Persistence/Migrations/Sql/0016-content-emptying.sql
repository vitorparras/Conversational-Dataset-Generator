-- Emptying an item (#73): the event of the chain, the grant that lets the application empty, and the two refusals the database keeps for itself.
--
-- The event carries a reference: the opaque number of the protocol outside the system that asked for the emptying, and nothing of the person who asked. It is a column of its own,
-- covered by the digest when it is present and left out of it when it is absent, so that the digest of every event written before this step stays what it was.
ALTER TABLE public.custody_event ADD COLUMN reference text;
GRANT INSERT (reference) ON public.custody_event TO PUBLIC;

-- The digest with the reference. Without one it is the digest of the step 0008; with one, the reference follows the rest, with its length in front, as every text does.
CREATE FUNCTION public.custody_digest(
    previous bytea, sequence bigint, occurred_at timestamptz, actor_person_id bigint, actor_system boolean,
    item_kind text, item_id bigint, step text, reason text, corrects bigint, recorded_by text, reference text)
RETURNS bytea
LANGUAGE plpgsql
IMMUTABLE
AS $$
BEGIN
    IF previous IS NULL OR pg_catalog.octet_length(previous) <> 32 OR sequence IS NULL OR occurred_at IS NULL OR actor_system IS NULL
        OR item_kind IS NULL OR item_id IS NULL OR step IS NULL OR reason IS NULL OR recorded_by IS NULL THEN
        RAISE EXCEPTION 'custody_digest: the input is outside the contract' USING ERRCODE = 'invalid_parameter_value';
    END IF;

    RETURN pg_catalog.sha256(
        previous
        || pg_catalog.int8send(sequence)
        || pg_catalog.timestamptz_send(occurred_at)
        || CASE WHEN actor_person_id IS NULL THEN '\x00'::bytea ELSE '\x01'::bytea || pg_catalog.int8send(actor_person_id) END
        || CASE WHEN actor_system THEN '\x01'::bytea ELSE '\x00'::bytea END
        || pg_catalog.int4send(pg_catalog.octet_length(pg_catalog.convert_to(item_kind, 'UTF8'))) || pg_catalog.convert_to(item_kind, 'UTF8')
        || pg_catalog.int8send(item_id)
        || pg_catalog.int4send(pg_catalog.octet_length(pg_catalog.convert_to(step, 'UTF8'))) || pg_catalog.convert_to(step, 'UTF8')
        || pg_catalog.int4send(pg_catalog.octet_length(pg_catalog.convert_to(reason, 'UTF8'))) || pg_catalog.convert_to(reason, 'UTF8')
        || CASE WHEN corrects IS NULL THEN '\x00'::bytea ELSE '\x01'::bytea || pg_catalog.int8send(corrects) END
        || pg_catalog.int4send(pg_catalog.octet_length(pg_catalog.convert_to(recorded_by, 'UTF8'))) || pg_catalog.convert_to(recorded_by, 'UTF8')
        || CASE WHEN reference IS NULL THEN '\x'::bytea
                ELSE '\x01'::bytea || pg_catalog.int4send(pg_catalog.octet_length(pg_catalog.convert_to(reference, 'UTF8'))) || pg_catalog.convert_to(reference, 'UTF8') END);
END
$$;

-- The digest of the step 0008 is the new one without a reference, so that there is one encoding and not two.
CREATE OR REPLACE FUNCTION public.custody_digest(
    previous bytea, sequence bigint, occurred_at timestamptz, actor_person_id bigint, actor_system boolean,
    item_kind text, item_id bigint, step text, reason text, corrects bigint, recorded_by text)
RETURNS bytea
LANGUAGE sql
IMMUTABLE
RETURN public.custody_digest(previous, sequence, occurred_at, actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, recorded_by, NULL::text);

-- The link of the step 0013 as it was, digesting with the reference.
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

    IF NEW.step NOT IN ('verify_chain', 'raw_text_read', 'sanitized_text_read', 'reversal_entries_read') AND public.custody_chain_broken() THEN
        RAISE EXCEPTION 'the custody chain failed its last verification: only a verification or a reading is added until the chain is restored'
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

-- The lists grow with the ones the code declares (CustodyNames). An emptying is about an item of the content; it takes one of the two reasons the request may give, and only a person does it.
-- A refusal to empty is an event too, about the same item, with the reason it was refused for: the author does not hold the function, or the item was emptied already.
-- The reference is a protocol number: letters, digits and . _ / - only, up to 64, so that no name and no piece of the personal data of whoever asked can be written in it.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_reason_listed,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused')),
    ADD CONSTRAINT custody_event_reason_listed CHECK (reason IN
        ('administrative_act', 'correction_of_record', 'content_read', 'verification_passed', 'verification_failed',
         'removal_requested_by_subject', 'contract_requirement', 'author_without_function', 'item_already_emptied')),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (reference IS NOT NULL) = (step IN ('item_emptied', 'item_emptying_refused'))
        AND (reference IS NULL OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));

-- The application empties: it sets the marker and clears the columns of an item and of its map, and changes nothing else of the content. It does so after the event is in the chain, in the
-- same transaction (ContentEmptier); the shape of an emptied row is the CHECK of the step 0010.
GRANT UPDATE (emptied_at, raw_text, sanitized_text, pseudonymized_text) ON content.item_text TO app;
GRANT UPDATE (emptied_at, entries) ON content.reversal_map TO app;

-- An emptied row is final: nothing in it changes again, so that the columns cleared do not come back and the marker does not go away. SQLSTATE CE004, a message that names no content.
CREATE FUNCTION public.refuse_change_of_emptied_content() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'the content was emptied: a row emptied does not change again'
        USING ERRCODE = 'CE004', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
END
$refuse$;

CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL) EXECUTE FUNCTION public.refuse_change_of_emptied_content();
CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.reversal_map
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL) EXECUTE FUNCTION public.refuse_change_of_emptied_content();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER emptied_content_is_final;
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER emptied_content_is_final;

-- A map is not added to an item that was emptied: what it would hold is what the emptying took away. The item is seen through the row policy, which is the one of the map's domain.
CREATE FUNCTION public.refuse_map_of_emptied_item() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF EXISTS (SELECT 1 FROM content.item_text i WHERE i.id = NEW.item_id AND i.domain_id = NEW.domain_id AND i.emptied_at IS NOT NULL) THEN
        RAISE EXCEPTION 'the item was emptied: no map is added to it'
            USING ERRCODE = 'CE004', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE TRIGGER map_needs_item_not_emptied BEFORE INSERT ON content.reversal_map
    FOR EACH ROW EXECUTE FUNCTION public.refuse_map_of_emptied_item();
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER map_needs_item_not_emptied;
