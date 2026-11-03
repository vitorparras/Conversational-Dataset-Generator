-- The window of the owner credential (#64): while a migration runs with the credential that owns the tables, the chain says so. The step owner_window_opened is written by the system, with the owner
-- credential, before the first step that is pending, and carries in its reference what the window is for (the version it leaves and the one it reaches) and the tip of the chain it found (sequence
-- and digest). The step owner_window_closed is written by the application at the next start, after the chain was verified and the guarantees of the server were conferred, and carries the verdict:
-- the item is the mask of what is missing (bits 1 to 32 of the conference, bit 64 for a chain that did not hold) and the reference points at the events that opened the window and the conferences.
-- The reference is a line of lowercase letters, digits, = and ; only, so that no name and no piece of the personal data gets into a record nobody removes. The emptying keeps its protocol number.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_verification_shape,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed')),
    ADD CONSTRAINT custody_event_verification_shape CHECK (
        (step IN ('verify_chain', 'verify_guarantees', 'owner_window_opened', 'owner_window_closed')) = (item_kind = 'custody_chain')
        AND (step IN ('verify_chain', 'verify_guarantees', 'owner_window_closed')) = (reason IN ('verification_passed', 'verification_failed'))
        AND (step NOT IN ('verify_chain', 'verify_guarantees', 'owner_window_opened', 'owner_window_closed') OR actor_system)
        AND (step <> 'verify_guarantees' OR (corrects IS NULL AND item_id BETWEEN 0 AND 63 AND (reason = 'verification_passed') = (item_id = 0)))
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed') OR corrects IS NULL)
        AND (step <> 'owner_window_opened' OR (reason = 'administrative_act' AND item_id >= 0))
        AND (step <> 'owner_window_closed' OR (item_id BETWEEN 0 AND 127 AND (reason = 'verification_passed') = (item_id = 0)))),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (reference IS NOT NULL) = (step IN ('item_emptied', 'item_emptying_refused', 'owner_window_opened', 'owner_window_closed'))
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));

-- The window that is open is found without reading the chain.
CREATE INDEX custody_event_owner_window_idx ON public.custody_event (sequence DESC) WHERE step IN ('owner_window_opened', 'owner_window_closed');

-- The link of the step 0016 as it was, with one more thing let through a chain that failed its verification: the window of the owner is recorded whatever the state of the chain, because the way out of a
-- break is a restoration and a migration is one of the ways to it. The verdict that passed stays refused after a failure.
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

    IF NEW.step NOT IN ('verify_chain', 'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'owner_window_opened', 'owner_window_closed') AND public.custody_chain_broken() THEN
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
