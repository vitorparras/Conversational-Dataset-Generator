-- The technical administration corrects the e-mail address of another person after the registration (issue 635, decision 11 of the owner): the step person_email_corrected records the act, by the
-- person of the administration, about the person whose address changed, with the reason of an administrative act and no reference, so that the address itself never enters the chain (policy 009).
-- A person who changes their own address on the account writes no step, as before. Only the step list changes, from the last one the earlier steps left (the 0042): every step it lists stays, and
-- the new one comes in beside them, with a check of its own shape.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'origin_read', 'person_email_corrected')),
    ADD CONSTRAINT custody_event_email_corrected_shape CHECK (
        step <> 'person_email_corrected'
        OR (item_kind = 'person' AND actor_person_id IS NOT NULL AND reason = 'administrative_act' AND corrects IS NULL AND reference IS NULL));
