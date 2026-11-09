-- The destination of each occurrence found in an item (issue 84, second part): what the language service found, as the kind and the offsets in code points, and what becomes of it, one of three. They are
-- written in the very statement that adds the item, so that no item is added in one transaction and its occurrences in another, and they never change: the one change of a row of content is still its emptying.
--
-- The offsets are sealed with the item, under its own key and the purpose of the raw text (decision 0030, which keeps in clear only the pseudonymized text): the offsets with the raw text rebuild what was found
-- (decision 0011), and the length and the place of a name are already a part of it. Emptying the item clears them with the rest, and reading them is an event of the chain, like reading any other sealed column.
-- A row that comes in without any (an item added before its occurrences were decided) holds none, and a reader finds nothing there; it never finds a default.
ALTER TABLE content.item_text ADD COLUMN occurrences bytea;

COMMENT ON COLUMN content.item_text.occurrences IS 'protected:texto-bruto';

-- An emptied item holds none: the shape of an emptied row (the step 0010) now lists the column too.
ALTER TABLE content.item_text
    DROP CONSTRAINT item_text_emptied,
    ADD CONSTRAINT item_text_emptied CHECK ((emptied_at IS NULL AND raw_text IS NOT NULL)
        OR (emptied_at IS NOT NULL AND raw_text IS NULL AND sanitized_text IS NULL AND pseudonymized_text IS NULL AND occurrences IS NULL));

-- The application clears the column with the others when it empties; it has no other right over it.
GRANT UPDATE (occurrences) ON content.item_text TO app;

-- A row not emptied keeps its occurrences whoever writes, as it keeps its texts (the step 0017): the trigger lets an update through only when it empties the row.
DROP TRIGGER emptied_content_is_final ON content.item_text;
CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL
        OR (NEW.emptied_at IS NULL
            AND (NEW.raw_text IS DISTINCT FROM OLD.raw_text OR NEW.sanitized_text IS DISTINCT FROM OLD.sanitized_text OR NEW.pseudonymized_text IS DISTINCT FROM OLD.pseudonymized_text
                OR NEW.occurrences IS DISTINCT FROM OLD.occurrences)))
    EXECUTE FUNCTION public.refuse_change_of_emptied_content();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER emptied_content_is_final;

-- Reading the occurrences is an event of the chain about the item, the step occurrences_read; only the list of the steps grows (the last one is the step 0041's), and every clause the chain fixes stays.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'occurrences_read'));

-- Reading the destinations of an item is a reading like the others (the 0013), so it goes on while the chain is broken, recorded in the chain: the function is the 0022 as it was, with the new step among the
-- readings the chain accepts after a failed verification. Everything else it does is untouched.
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

    IF NEW.step NOT IN ('verify_chain', 'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'occurrences_read', 'sign_in_accepted', 'sign_in_refused', 'credential_recomputed')
        AND public.custody_chain_broken() THEN
        RAISE EXCEPTION 'the custody chain failed its last verification: only a verification, a reading or an entry is added until the chain is restored'
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
