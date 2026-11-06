-- A verification of the chain that someone asked for says who asked (issue 605, CA-4): the event verify_chain may carry, in the reference, requested-by/<id> of whoever asked, and has none when the host
-- ran the verification by itself. The event stays written by the system, as the 0012 requires of the step; the reference is the one place the asker is kept, and the chain hashes it with the rest.
-- Only the shape of the reference changes, from the last one the earlier migrations left (the 0036, which brings the batch and the change of the own password beside everything the 0033 listed):
-- every step and every clause they fix stays, and verify_chain leaves the rule that wants a reference exactly in the steps listed, to have it only in this form or none.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (step IN ('recovery_tested', 'verify_chain')
            OR (reference IS NOT NULL)
                = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored',
                            'restore_rehearsed', 'domain_label_revoked', 'password_changed') OR reason = 'batch_rejected'))
        AND (step <> 'verify_chain' OR reference IS NULL OR reference ~ '^requested-by/[1-9][0-9]{0,17}$')
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));
