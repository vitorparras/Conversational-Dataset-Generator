-- The end of a restore (#64, CA-5d) is an event of the chain, written by the system: the item is the rows put back, the reason says whether the chain and the guarantees held, and the reference
-- says which copy (the digest of its manifest) and the tip before and after. Only the checks change, from the last ones the 0026 left: every step they list and every clause they fix stays,
-- and the step restored comes in beside them.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_verification_shape,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored')),
    ADD CONSTRAINT custody_event_verification_shape CHECK (
        (step IN ('verify_chain', 'verify_guarantees', 'seal_recorded', 'owner_window_opened', 'owner_window_closed', 'restored')) = (item_kind = 'custody_chain')
        AND (step IN ('verify_chain', 'verify_guarantees', 'owner_window_closed', 'restored')) = (reason IN ('verification_passed', 'verification_failed'))
        AND (step NOT IN ('verify_chain', 'verify_guarantees', 'owner_window_opened', 'owner_window_closed', 'restored') OR actor_system)
        AND (step NOT IN ('verify_guarantees', 'owner_window_closed') OR (corrects IS NULL AND item_id BETWEEN 0 AND 63 AND (reason = 'verification_passed') = (item_id = 0)))
        AND (step <> 'owner_window_opened' OR (corrects IS NULL AND reason = 'administrative_act' AND item_id >= 0))
        AND (step <> 'restored' OR (corrects IS NULL AND item_id >= 0))),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (reference IS NOT NULL) = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored'))
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));
