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
