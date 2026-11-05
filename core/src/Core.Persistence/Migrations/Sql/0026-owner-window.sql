-- The window of the owner credential (#64): owner_window_opened is written by the system with the owner credential before the first pending step, and owner_window_closed by the application at
-- the next start, after the chain was verified and the guarantees conferred. The item of the opening is the version reached; the item of the closing is the mask of the guarantees missing.
-- The reference is a line of lowercase letters, digits, = and ; only, so that no personal data gets into a record nobody removes. The emptying keeps its protocol number.
-- Only the checks change, from the last ones the earlier steps left (the step list and the shape of a verification from the 0025, the shape of a reference from the 0023): every step they list
-- and every clause they fix stays, and the two steps of the window come in beside them.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_verification_shape,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed')),
    ADD CONSTRAINT custody_event_verification_shape CHECK (
        (step IN ('verify_chain', 'verify_guarantees', 'seal_recorded', 'owner_window_opened', 'owner_window_closed')) = (item_kind = 'custody_chain')
        AND (step IN ('verify_chain', 'verify_guarantees', 'owner_window_closed')) = (reason IN ('verification_passed', 'verification_failed'))
        AND (step NOT IN ('verify_chain', 'verify_guarantees', 'owner_window_opened', 'owner_window_closed') OR actor_system)
        AND (step NOT IN ('verify_guarantees', 'owner_window_closed') OR (corrects IS NULL AND item_id BETWEEN 0 AND 63 AND (reason = 'verification_passed') = (item_id = 0)))
        AND (step <> 'owner_window_opened' OR (corrects IS NULL AND reason = 'administrative_act' AND item_id >= 0))),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (reference IS NOT NULL) = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed'))
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));

-- The window that is open is found without reading the chain.
CREATE INDEX custody_event_owner_window_idx ON public.custody_event (sequence DESC) WHERE step IN ('owner_window_opened', 'owner_window_closed');
