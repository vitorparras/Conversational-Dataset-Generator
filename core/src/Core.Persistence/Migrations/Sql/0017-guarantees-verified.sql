-- The conference of the guarantees of the server is an event of the chain (#68): the step verify_guarantees about the chain, written by the system, with the reason verification_passed or
-- verification_failed. The item is the mask of the guarantees missing, in the closed list of GuaranteeKinds (bits 1 to 32); zero means none is. Only the checks change, from the lists of 0016.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_verification_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'verify_guarantees')),
    -- The verdict is the mask: passed when nothing is missing, failed when something is, and a conference corrects no event.
    ADD CONSTRAINT custody_event_verification_shape CHECK (
        (step IN ('verify_chain', 'verify_guarantees')) = (item_kind = 'custody_chain')
        AND (step IN ('verify_chain', 'verify_guarantees')) = (reason IN ('verification_passed', 'verification_failed'))
        AND (step NOT IN ('verify_chain', 'verify_guarantees') OR actor_system)
        AND (step <> 'verify_guarantees' OR (corrects IS NULL AND item_id BETWEEN 0 AND 63 AND (reason = 'verification_passed') = (item_id = 0))));

CREATE INDEX custody_event_guarantees_idx ON public.custody_event (sequence DESC) WHERE step = 'verify_guarantees';

-- The domains that hold something to sample, so that the conference reads, with an event each, only what is there: a read that finds nothing leaves no event, and the audit of the server counts
-- it as a read without one. The database writes the row, as its owner, when the first item or map of a domain is added.
CREATE TABLE public.content_domain (domain_id integer NOT NULL REFERENCES public.domain (id), table_name text NOT NULL CHECK (table_name IN ('item_text', 'reversal_map')),
    PRIMARY KEY (domain_id, table_name));
GRANT SELECT ON public.content_domain TO PUBLIC;

CREATE FUNCTION public.record_content_domain() RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS $record$
BEGIN
    INSERT INTO public.content_domain (domain_id, table_name) VALUES (NEW.domain_id, TG_TABLE_NAME) ON CONFLICT DO NOTHING;
    RETURN NULL;
END
$record$;

CREATE TRIGGER content_domain_is_recorded AFTER INSERT ON content.item_text FOR EACH ROW EXECUTE FUNCTION public.record_content_domain();
CREATE TRIGGER content_domain_is_recorded AFTER INSERT ON content.reversal_map FOR EACH ROW EXECUTE FUNCTION public.record_content_domain();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER content_domain_is_recorded;
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER content_domain_is_recorded;

-- What was added before: the owner is held to the row policy too, so each domain is declared in turn.
DO $fill$
DECLARE declared integer;
BEGIN
    FOR declared IN SELECT id FROM public.domain LOOP
        PERFORM pg_catalog.set_config('app.domain', declared::text, true);
        INSERT INTO public.content_domain SELECT declared, 'item_text' WHERE EXISTS (SELECT 1 FROM content.item_text);
        INSERT INTO public.content_domain SELECT declared, 'reversal_map' WHERE EXISTS (SELECT 1 FROM content.reversal_map);
    END LOOP;
END
$fill$;
