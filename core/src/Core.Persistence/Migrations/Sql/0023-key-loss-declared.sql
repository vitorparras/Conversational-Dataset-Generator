-- The loss of a key is declared, and the declaration is an event of the chain (issue 67, part 1): the step key_loss_declared about the master key (item kind master_key), written by the system for
-- an administrative act. Its id is what was tried to recover the key (1 the safekeeping copy, 2 the copy before it, 4 the record of the creation of the domain, summed), and its reference is the scope:
-- master for the whole key, or the label, as the custody file writes it. Nothing else travels in the event, and nothing takes it back.
--
-- The lists grow and the reference now belongs to this step too; everything the 0016, the 0019, the 0020 and the 0021 fixed stays as they left it. Triggers, digest and grants stay as they were.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN ('person', 'item_text', 'reversal_map', 'custody_chain', 'content_epoch', 'domain', 'master_key')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated', 'key_loss_declared')),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (reference IS NOT NULL) = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated', 'key_loss_declared'))
        AND (reference IS NULL OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL)),
    ADD CONSTRAINT custody_event_key_loss_shape CHECK (
        (step = 'key_loss_declared') = (item_kind = 'master_key')
        AND (step <> 'key_loss_declared' OR (
            actor_system AND reason = 'administrative_act' AND corrects IS NULL AND item_id BETWEEN 1 AND 7
            AND coalesce(reference ~ '^(master|(texto-bruto|mapa-reversao)/[1-9][0-9]*/v[1-9][0-9]*)$', false))));
