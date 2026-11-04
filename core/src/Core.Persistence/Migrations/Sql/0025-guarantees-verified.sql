-- The conference of the guarantees of the server is an event of the chain (#68): the step verify_guarantees about the chain, written by the system, with the reason verification_passed or
-- verification_failed. The item is the mask of the guarantees missing, in the closed list of GuaranteeKinds (bits 1 to 32); zero means none is. Only the checks change, from the lists
-- the 0024 (the steps) and the 0020 (the shape of a verification and of a seal) left: every step they list stays, and the clauses of the other shapes stay as the earlier steps left them.
-- The last conference is found without reading the chain, as the last verification is (0013).
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_verification_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees')),
    -- Only a verification, a conference and a seal are about the chain, and no other step is; a verdict is given by a verification or a conference alone, and both are the system's.
    -- The verdict of a conference is the mask: passed when nothing is missing, failed when something is, and a conference corrects no event.
    ADD CONSTRAINT custody_event_verification_shape CHECK (
        (step IN ('verify_chain', 'verify_guarantees', 'seal_recorded')) = (item_kind = 'custody_chain')
        AND (step IN ('verify_chain', 'verify_guarantees')) = (reason IN ('verification_passed', 'verification_failed'))
        AND (step NOT IN ('verify_chain', 'verify_guarantees') OR actor_system)
        AND (step <> 'verify_guarantees' OR (corrects IS NULL AND item_id BETWEEN 0 AND 63 AND (reason = 'verification_passed') = (item_id = 0))));
CREATE INDEX custody_event_guarantees_idx ON public.custody_event (sequence DESC) WHERE step = 'verify_guarantees';
-- The domains that still hold something to sample, so that the conference reads only what is there: a read that finds nothing leaves no event and the audit of the server counts it as a read without one. The database keeps the count of what is not emptied, as its owner, and only the role of the application, which runs the conference, reads it.
-- The count is kept as entries that only add (+1 or -1) and the sum is what is held, so that no two writers of one domain wait for the same row (an update of it made an emptying wait for a write).
CREATE TABLE public.content_domain (domain_id integer NOT NULL REFERENCES public.domain (id), table_name text NOT NULL CHECK (table_name IN ('item_text', 'reversal_map')),
    delta bigint NOT NULL CHECK (delta <> 0));
GRANT SELECT ON public.content_domain TO app;
CREATE FUNCTION public.count_content_domain() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $count$
BEGIN
    INSERT INTO public.content_domain (domain_id, table_name, delta) VALUES (NEW.domain_id, TG_TABLE_NAME, CASE TG_OP WHEN 'INSERT' THEN 1 ELSE -1 END);
    RETURN NULL;
END
$count$;
CREATE TRIGGER content_domain_is_counted AFTER INSERT ON content.item_text FOR EACH ROW EXECUTE FUNCTION public.count_content_domain();
CREATE TRIGGER content_domain_is_counted AFTER INSERT ON content.reversal_map FOR EACH ROW EXECUTE FUNCTION public.count_content_domain();
CREATE TRIGGER content_domain_is_emptied AFTER UPDATE ON content.item_text FOR EACH ROW WHEN (OLD.emptied_at IS NULL AND NEW.emptied_at IS NOT NULL) EXECUTE FUNCTION public.count_content_domain();
CREATE TRIGGER content_domain_is_emptied AFTER UPDATE ON content.reversal_map FOR EACH ROW WHEN (OLD.emptied_at IS NULL AND NEW.emptied_at IS NOT NULL) EXECUTE FUNCTION public.count_content_domain();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER content_domain_is_counted;
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER content_domain_is_emptied;
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER content_domain_is_counted;
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER content_domain_is_emptied;
-- What was added before, domain by domain: the owner is held to the row policy too, so each domain is declared in turn.
DO $fill$
DECLARE declared integer;
BEGIN
    FOR declared IN SELECT id FROM public.domain LOOP
        PERFORM pg_catalog.set_config('app.domain', declared::text, true);
        INSERT INTO public.content_domain SELECT declared, 'item_text', count(*) FROM content.item_text WHERE emptied_at IS NULL HAVING count(*) > 0;
        INSERT INTO public.content_domain SELECT declared, 'reversal_map', count(*) FROM content.reversal_map WHERE emptied_at IS NULL HAVING count(*) > 0;
    END LOOP;
END
$fill$;
