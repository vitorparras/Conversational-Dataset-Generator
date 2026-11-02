-- Creating a domain is an event of the chain (issue 54, part 2): the step domain_created, about the domain (item kind domain, its id is the domain's own), written by the system with the
-- reason administrative_act. The owner credential that creates a domain is no person of the registry, so the author is the system; who created it stays in domain.created_by and in the audit
-- log of the server. The name of the domain is never in the event: the item is the number alone.
--
-- Only the two lists grow, and the shape of the new step is fixed as the rotation's is. Triggers, digest and grants stay as they were.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN ('person', 'item_text', 'reversal_map', 'custody_chain', 'content_epoch', 'domain')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created')),
    ADD CONSTRAINT custody_event_domain_shape CHECK (
        (step = 'domain_created') = (item_kind = 'domain')
        AND (step <> 'domain_created' OR (actor_system AND reason = 'administrative_act' AND reference IS NULL)));
