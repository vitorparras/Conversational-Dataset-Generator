-- Reading protected content is an event of the chain, about the item and the table it was read from, and the lists the database holds grow with the ones the code declares (CustodyNames).
-- Only the checks change: the triggers that keep the events from being changed or emptied are not touched.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_reason_listed,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN ('person', 'item_text', 'reversal_map')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read')),
    ADD CONSTRAINT custody_event_reason_listed CHECK (reason IN ('administrative_act', 'correction_of_record', 'content_read'));
