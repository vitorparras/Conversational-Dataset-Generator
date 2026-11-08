-- A person changes their own password (issue 597): a step of the chain of its own, password_changed, written in the same transaction as the new derivation. The author and the item are that person,
-- the reason is the administrative act, and the reference is how many of their other sessions the change ended, in digits, so that no value of any password and nothing free is written.
-- Only the checks change, from the last ones the earlier migrations left (the step list and the shape of the reference from the 0034, which brings the batch beside everything the 0033 listed):
-- every step they list and every clause they fix stays, and the new step comes in beside it. The record of the person (person_change) is the one a password set again already writes.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed')),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (step = 'recovery_tested'
            OR (reference IS NOT NULL)
                = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored',
                            'restore_rehearsed', 'domain_label_revoked', 'password_changed') OR reason = 'batch_rejected'))
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL)),
    ADD CONSTRAINT custody_event_password_changed_shape CHECK (
        step <> 'password_changed'
        OR (item_kind = 'person' AND coalesce(actor_person_id = item_id, false) AND reason = 'administrative_act' AND corrects IS NULL AND reference ~ '^[0-9]{1,6}$'));
