-- Issue 56: the next generation of a domain's two labels is added to derivation_label (never changed or dropped, 0045); the event is the system's, about the domain, referencing the generation (v2).
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_domain_shape,
    DROP CONSTRAINT custody_event_emptying_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated')),
    ADD CONSTRAINT custody_event_domain_shape CHECK (
        (step IN ('domain_created', 'domain_label_rotated')) = (item_kind = 'domain')
        AND (step NOT IN ('domain_created', 'domain_label_rotated') OR (actor_system AND reason = 'administrative_act' AND corrects IS NULL))
        AND (step <> 'domain_created' OR reference IS NULL)
        AND (step <> 'domain_label_rotated' OR coalesce(reference ~ '^v[1-9][0-9]*$', false))),
    ADD CONSTRAINT custody_event_emptying_shape CHECK (
        (reference IS NOT NULL) = (step IN ('item_emptied', 'item_emptying_refused', 'domain_label_rotated'))
        AND (reference IS NULL OR reference ~ '^[A-Za-z0-9._/-]{1,64}$')
        AND (step NOT IN ('item_emptied', 'item_emptying_refused') OR item_kind = 'item_text')
        AND (step = 'item_emptied') = (reason IN ('removal_requested_by_subject', 'contract_requirement'))
        AND (step = 'item_emptying_refused') = (reason IN ('author_without_function', 'item_already_emptied'))
        AND (step <> 'item_emptied' OR actor_person_id IS NOT NULL));
