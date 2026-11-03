-- The rotation of the label of a domain (issue 56, part 1): the next generation of the two labels of a domain is written to derivation_label as the creation writes the first. derivation_label stays
-- as the decision 0045 left it: a label is never changed nor dropped, and nothing here touches it.

-- The event is about a domain, by its number, as the creation is, and is the system's for an administrative act: the owner credential that rotates is no person of the registry.
-- The reference carries the generation the domain passes to write under (v2), from which the two labels follow, since the purposes are closed and the domain is the item. It keeps the character
-- set of the protocol number, and the name of the domain is not in it.
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
