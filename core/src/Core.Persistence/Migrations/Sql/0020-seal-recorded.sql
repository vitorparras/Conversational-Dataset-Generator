-- Recording the seal of the chain is an event of the chain (issue 62, part A): the step seal_recorded about the chain itself (item kind custody_chain), whose id is the sequence of the event sealed,
-- which is also the number of events the seal counts. The digest of the event sealed is not repeated in the event: it is read from the chain, by that sequence. The author is the system or an active
-- person who confirmed the record, and the reason is administrative_act; nothing else travels in the event.
--
-- Only the step list grows, and the shape check of the events about the chain now covers the two steps that are about it. Triggers, digest and grants stay as they were: while the last verification
-- failed, the trigger of the link refuses this step as it refuses every step but a verification and a reading.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_verification_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded')),
    -- Only a verification and a seal are about the chain, and no other step is; a verdict is given by a verification alone, and a verification is the system's.
    ADD CONSTRAINT custody_event_verification_shape CHECK (
        (step IN ('verify_chain', 'seal_recorded')) = (item_kind = 'custody_chain')
        AND (step = 'verify_chain') = (reason IN ('verification_passed', 'verification_failed'))
        AND (step <> 'verify_chain' OR actor_system)),
    ADD CONSTRAINT custody_event_seal_shape CHECK (
        step <> 'seal_recorded' OR (reason = 'administrative_act' AND corrects IS NULL AND reference IS NULL));
