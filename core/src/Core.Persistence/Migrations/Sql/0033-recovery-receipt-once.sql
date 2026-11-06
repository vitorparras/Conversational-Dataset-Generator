-- The same receipt of a rehearsal of the recovery is not recorded twice (issue 584): the event recovery_tested may carry, in the reference, the digest of the receipt it records (SHA-256 of its text,
-- 64 lowercase hexadecimal digits), and the server refuses a second event with the same digest, so that two records of the same receipt at the same time cannot both get in.
-- An event of the step without a reference, as the 0024 allowed, stays valid; the clauses of the loss and the other clauses of the shape stay as the 0024 left them.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_key_loss_shape,
    ADD CONSTRAINT custody_event_key_loss_shape CHECK (
        (step IN ('key_loss_declared', 'recovery_tested')) = (item_kind = 'master_key')
        AND (step <> 'key_loss_declared' OR (
            actor_system AND reason = 'administrative_act' AND corrects IS NULL AND item_id BETWEEN 1 AND 7
            AND coalesce(reference ~ '^(master|(texto-bruto|mapa-reversao)/[1-9][0-9]*/v[1-9][0-9]*)$', false)))
        AND (step <> 'recovery_tested' OR (
            actor_system AND reason = 'administrative_act' AND corrects IS NULL AND item_id >= 1
            AND (reference IS NULL OR reference ~ '^[0-9a-f]{64}$'))));
CREATE UNIQUE INDEX custody_event_recovery_receipt_once ON public.custody_event (reference) WHERE step = 'recovery_tested' AND reference IS NOT NULL;
