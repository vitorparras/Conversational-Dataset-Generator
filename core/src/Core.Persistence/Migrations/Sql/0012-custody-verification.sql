-- The verification of the chain is an event of the chain: the step verify_chain about the chain itself (item kind custody_chain), written by the system, with the reason
-- verification_passed or verification_failed. The item is the reference: its id is the number of events the verification read, and, when it failed, the event it points at (corrects) is the first inconsistent one.
-- Only the checks change here, from the lists of the step before: the triggers that keep the events from being changed or emptied (0009) are not touched.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_reason_listed,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN ('person', 'item_text', 'reversal_map', 'custody_chain')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain')),
    ADD CONSTRAINT custody_event_reason_listed CHECK (reason IN
        ('administrative_act', 'correction_of_record', 'content_read', 'verification_passed', 'verification_failed')),
    -- A verification is about the chain, by the system and with a verdict; nothing else is about the chain, and no verdict is given for anything else.
    ADD CONSTRAINT custody_event_verification_shape CHECK (
        (step = 'verify_chain') = (item_kind = 'custody_chain')
        AND (step = 'verify_chain') = (reason IN ('verification_passed', 'verification_failed'))
        AND (step <> 'verify_chain' OR actor_system));

-- The last verification is found without reading the chain.
CREATE INDEX custody_event_verification_idx ON public.custody_event (sequence DESC) WHERE step = 'verify_chain';

-- Whether the last verification recorded failed. A chain that was never verified is not broken.
CREATE FUNCTION public.custody_chain_broken() RETURNS boolean
LANGUAGE sql STABLE
AS $broken$
    SELECT COALESCE((SELECT e.reason = 'verification_failed' FROM public.custody_event e WHERE e.step = 'verify_chain' ORDER BY e.sequence DESC LIMIT 1), false)
$broken$;

-- The step before, with one refusal added: while the last verification failed, only a new verification and the reading of content are added; every other event is refused. SQLSTATE CE002, a message that names no event.
CREATE OR REPLACE FUNCTION public.custody_event_link() RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    tip_sequence bigint;
    tip_digest bytea;
    author_active boolean;
BEGIN
    -- A verdict that passed was made on a view of the chain: one that read fewer events than the sequence of the last failure recorded was made before that failure, and lifting the refusal with it
    -- would hide a break that is still there. A verification made after the failure reads the failure itself, so it is never refused here.
    IF NEW.step = 'verify_chain' AND NEW.reason = 'verification_passed'
        AND NEW.item_id < COALESCE((SELECT e.sequence FROM public.custody_event e WHERE e.step = 'verify_chain' AND e.reason = 'verification_failed' ORDER BY e.sequence DESC LIMIT 1), 0) THEN
        RAISE EXCEPTION 'the verification passed was made on a view older than the last failure recorded: it is not added'
            USING ERRCODE = 'CE003', SCHEMA = 'public', TABLE = 'custody_event';
    END IF;

    IF NEW.step NOT IN ('verify_chain', 'raw_text_read', 'sanitized_text_read', 'reversal_entries_read') AND public.custody_chain_broken() THEN
        RAISE EXCEPTION 'the custody chain failed its last verification: only a verification or a reading is added until one passes'
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
        NEW.item_kind, NEW.item_id, NEW.step, NEW.reason, NEW.corrects, NEW.recorded_by);
    RETURN NEW;
END
$$;

-- Content is not written either while the last verification failed: the chain is one for the installation, and so is the refusal. Reading goes on.
CREATE FUNCTION public.refuse_content_write_while_chain_broken() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF public.custody_chain_broken() THEN
        RAISE EXCEPTION 'the custody chain failed its last verification: no content is written until a verification passes'
            USING ERRCODE = 'CE002', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NULL;
END
$refuse$;

CREATE TRIGGER content_write_needs_intact_chain
    BEFORE INSERT OR UPDATE OR DELETE OR TRUNCATE ON content.item_text
    FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_content_write_while_chain_broken();
CREATE TRIGGER content_write_needs_intact_chain
    BEFORE INSERT OR UPDATE OR DELETE OR TRUNCATE ON content.reversal_map
    FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_content_write_while_chain_broken();

-- ALWAYS, so the refusal also holds in a session that set session_replication_role to replica.
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER content_write_needs_intact_chain;
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER content_write_needs_intact_chain;

-- The mold of a content table, as it was, with the same refusal: a table born later is born with the trigger.
CREATE OR REPLACE FUNCTION public.create_content_table(table_name text, columns text) RETURNS void
LANGUAGE plpgsql
AS $mold$
BEGIN
    EXECUTE format(
        'CREATE TABLE content.%I (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, domain_id integer NOT NULL REFERENCES public.domain (id), %s)',
        table_name, columns);
    EXECUTE format('ALTER TABLE content.%I ENABLE ROW LEVEL SECURITY', table_name);
    EXECUTE format('ALTER TABLE content.%I FORCE ROW LEVEL SECURITY', table_name);
    EXECUTE format(
        'CREATE POLICY domain_isolation ON content.%I USING (domain_id = public.declared_domain()) WITH CHECK (domain_id = public.declared_domain())',
        table_name);
    EXECUTE format(
        'CREATE TRIGGER content_write_needs_intact_chain BEFORE INSERT OR UPDATE OR DELETE OR TRUNCATE ON content.%I '
        || 'FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_content_write_while_chain_broken()',
        table_name);
    EXECUTE format('ALTER TABLE content.%I ENABLE ALWAYS TRIGGER content_write_needs_intact_chain', table_name);
END
$mold$;
