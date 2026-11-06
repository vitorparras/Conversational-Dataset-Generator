-- The same receipt of a rehearsal of the recovery is not recorded twice (issue 584): the event recovery_tested may carry, in the reference, the digest of the receipt it records (SHA-256 of its text,
-- 64 lowercase hexadecimal digits), and the server refuses a second event with the same digest, so that two records of the same receipt at the same time cannot both get in.
-- An event of the step without a reference, as the 0024 allowed, stays valid. Two clauses spoke of the reference of this step: the shape of the loss of a key (the 0024) and the shape of the
-- reference (the 0030, which wants a reference exactly in the steps it lists, and this one is not among them), so both are made again; every clause of the other steps stays as it was.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_key_loss_shape,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_key_loss_shape CHECK (
        (step IN ('key_loss_declared', 'recovery_tested')) = (item_kind = 'master_key')
        AND (step <> 'key_loss_declared' OR (
            actor_system AND reason = 'administrative_act' AND corrects IS NULL AND item_id BETWEEN 1 AND 7
            AND coalesce(reference ~ '^(master|(texto-bruto|mapa-reversao)/[1-9][0-9]*/v[1-9][0-9]*)$', false)))
        AND (step <> 'recovery_tested' OR (
            actor_system AND reason = 'administrative_act' AND corrects IS NULL AND item_id >= 1
            AND (reference IS NULL OR reference ~ '^[0-9a-f]{64}$')))),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (step = 'recovery_tested'
            OR (reference IS NOT NULL)
                = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared', 'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed',
                            'domain_label_revoked')))
        AND (reference IS NULL OR step IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('owner_window_opened', 'owner_window_closed', 'restored') OR reference ~ '^[a-z0-9=;]{1,160}$')
        AND (step <> 'restore_rehearsed' OR reference ~ '^[a-z]+(\.[a-z]+)*$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));
CREATE UNIQUE INDEX custody_event_recovery_receipt_once ON public.custody_event (reference) WHERE step = 'recovery_tested' AND reference IS NOT NULL;
