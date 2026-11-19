-- The sanitization of a batch is an event of the record (issue 87): the chain learns the step batch_sanitized, written when the submission closes the items, by whoever submitted them. The item is the
-- batch and the reference is the version of the list of steps that treated it, the SHA-256 in lowercase hex, and never a word of the content. The checks change from the ones the 0054 and the 0050 left,
-- and the step joins the ones that always carry a reference.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_batch_shape,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'origin_read', 'configuration_approved', 'configuration_refused',
         'configuration_in_force', 'person_email_corrected', 'configuration_not_applied', 'occurrences_read', 'batch_sampled', 'batch_reviewed', 'batch_sanitized')),
    ADD CONSTRAINT custody_event_batch_shape CHECK (
        (step IN ('batch_opened', 'batch_closed', 'batch_sampled', 'batch_reviewed', 'batch_sanitized', 'origin_read')) = (item_kind = 'ingestion_batch')
        AND (step = 'batch_closed') = (reason IN ('batch_accepted', 'batch_rejected'))
        AND (step = 'batch_reviewed') = (reason IN ('review_clean', 'review_found'))
        AND (step NOT IN ('batch_opened', 'batch_sampled') OR (reason = 'administrative_act' AND actor_person_id IS NOT NULL))
        AND (step <> 'batch_reviewed' OR actor_person_id IS NOT NULL)
        AND (step <> 'origin_read' OR (reason = 'content_read' AND reference IS NULL))
        AND (step NOT IN ('batch_opened', 'batch_closed', 'batch_sampled', 'batch_reviewed', 'batch_sanitized', 'origin_read') OR corrects IS NULL)
        AND (step <> 'batch_sanitized' OR (reason = 'administrative_act' AND reference ~ '^[0-9a-f]{64}$'))
        AND (step <> 'batch_closed' OR (reason = 'batch_rejected') = (reference IS NOT NULL))
        AND (step <> 'batch_closed' OR reference IS NULL OR reference ~ '^[a-z][a-z0-9_]{0,62}$')),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (step IN ('recovery_tested', 'verify_chain')
            OR (reference IS NOT NULL)
                = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored',
                            'restore_rehearsed', 'domain_label_revoked', 'password_changed', 'configuration_approved', 'configuration_refused', 'configuration_in_force',
                            'configuration_not_applied', 'batch_sanitized')
                    OR reason = 'batch_rejected'))
        AND (step <> 'verify_chain' OR reference IS NULL OR reference ~ '^requested-by/[1-9][0-9]{0,17}$')
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));
