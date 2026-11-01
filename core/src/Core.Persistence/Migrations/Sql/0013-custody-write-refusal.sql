-- The refusal of writing after a verification that failed (#61): while the last verdict recorded is a failure, the chain accepts no event but a verification and the reading of content, a verdict that
-- passed is refused until a restoration, and content is not written. The checks and the step verify_chain are the ones of 0012; the triggers that keep the events from being changed or emptied (0009) are not touched.

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
    -- After a failure the chain does not close again without a restoration: there is no way to rewrite it (requirement 019), so a verdict that passed after a verdict that failed is necessarily false.
    -- It is refused whoever inserts it, the application included, and whatever view it was made on; the way out of a break is the restoration, which brings the state of the copy. SQLSTATE CE003.
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
        RAISE EXCEPTION 'the custody chain failed its last verification: no content is written until the chain is restored'
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
