-- The result of the rehearsal of a restore is an event of the chain (#66): the step restore_rehearsed about the chain, written by the system, with the reason verification_passed or
-- verification_failed. Its id is the mask of what did not hold, in the closed list of RehearsalFindings (bits 1 to 16 are the guarantees of 0023, 64 the links, 128 the seal); zero means
-- everything held. Its reference names what did not hold, apart by a dot, or says held. Only the checks change, from the lists of 0024. While the last verification failed the chain takes no
-- such event, as it takes no other.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_verification_shape,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated', 'key_loss_declared', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restore_rehearsed')),
    ADD CONSTRAINT custody_event_verification_shape CHECK (
        (step IN ('verify_chain', 'seal_recorded', 'verify_guarantees', 'owner_window_opened', 'owner_window_closed', 'restore_rehearsed')) = (item_kind = 'custody_chain')
        AND (step IN ('verify_chain', 'verify_guarantees', 'owner_window_closed', 'restore_rehearsed')) = (reason IN ('verification_passed', 'verification_failed'))
        AND (step NOT IN ('verify_chain', 'verify_guarantees', 'owner_window_opened', 'owner_window_closed', 'restore_rehearsed') OR actor_system)
        AND (step NOT IN ('verify_guarantees', 'owner_window_closed') OR (corrects IS NULL AND item_id BETWEEN 0 AND 63 AND (reason = 'verification_passed') = (item_id = 0)))
        AND (step <> 'owner_window_opened' OR (corrects IS NULL AND reason = 'administrative_act' AND item_id >= 0))
        AND (step <> 'restore_rehearsed' OR (corrects IS NULL AND item_id BETWEEN 0 AND 255 AND (reason = 'verification_passed') = (item_id = 0)))),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (reference IS NOT NULL)
            = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restore_rehearsed'))
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));
