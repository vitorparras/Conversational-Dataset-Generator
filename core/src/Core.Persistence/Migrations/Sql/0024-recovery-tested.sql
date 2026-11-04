-- The rehearsal of the recovery is an event of the chain (issue 67, part 2): the step recovery_tested about the master key (item kind master_key, as the declaration of a loss is), written by the
-- system for an administrative act, its id the sequence of the top of the copy that was opened and no reference. It is recorded only after the receipt of the rehearsal was checked against the chain
-- and the key of the production, which the host does before it writes.
--
-- The step list grows from the one the 0023 left (with sign_in_accepted, sign_in_refused and credential_recomputed in it)
-- and the shape of the events about the master key now tells the two steps apart; the item kinds, the shape of an entry and the clauses of the loss stay as the 0022 and the 0023 left them. Triggers, digest and grants stay.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_key_loss_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested')),
    ADD CONSTRAINT custody_event_key_loss_shape CHECK (
        (step IN ('key_loss_declared', 'recovery_tested')) = (item_kind = 'master_key')
        AND (step <> 'key_loss_declared' OR (
            actor_system AND reason = 'administrative_act' AND corrects IS NULL AND item_id BETWEEN 1 AND 7
            AND coalesce(reference ~ '^(master|(texto-bruto|mapa-reversao)/[1-9][0-9]*/v[1-9][0-9]*)$', false)))
        AND (step <> 'recovery_tested' OR (actor_system AND reason = 'administrative_act' AND corrects IS NULL AND reference IS NULL AND item_id >= 1)));
