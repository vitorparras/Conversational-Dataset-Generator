-- The destination of each occurrence found in an item (issue 84, second part): what the language service found, as the kind and the offsets in code points, and what becomes of it, one of three. They are
-- written in the very statement that adds the item, so that no item is added in one transaction and its occurrences in another, and they never change: the one change of a row of content is still its emptying.
--
-- The offsets are sealed with the item, under its own key and the purpose of the raw text (decision 0030, which keeps in clear only the pseudonymized text): the offsets with the raw text rebuild what was found
-- (decision 0011), and the length and the place of a name are already a part of it. Emptying the item clears them with the rest, and reading them is an event of the chain, like reading any other sealed column.
-- A row that comes in without any (an item added before its occurrences were decided) holds none, and a reader finds nothing there; it never finds a default.
ALTER TABLE content.item_text ADD COLUMN occurrences bytea;

COMMENT ON COLUMN content.item_text.occurrences IS 'protected:texto-bruto';

-- An emptied item holds none: the shape of an emptied row (the step 0010) now lists the column too.
ALTER TABLE content.item_text
    DROP CONSTRAINT item_text_emptied,
    ADD CONSTRAINT item_text_emptied CHECK ((emptied_at IS NULL AND raw_text IS NOT NULL)
        OR (emptied_at IS NOT NULL AND raw_text IS NULL AND sanitized_text IS NULL AND pseudonymized_text IS NULL AND occurrences IS NULL));

-- The application clears the column with the others when it empties; it has no other right over it.
GRANT UPDATE (occurrences) ON content.item_text TO app;

-- A row not emptied keeps its occurrences whoever writes, as it keeps its texts (the step 0017): the trigger lets an update through only when it empties the row.
DROP TRIGGER emptied_content_is_final ON content.item_text;
CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL
        OR (NEW.emptied_at IS NULL
            AND (NEW.raw_text IS DISTINCT FROM OLD.raw_text OR NEW.sanitized_text IS DISTINCT FROM OLD.sanitized_text OR NEW.pseudonymized_text IS DISTINCT FROM OLD.pseudonymized_text
                OR NEW.occurrences IS DISTINCT FROM OLD.occurrences)))
    EXECUTE FUNCTION public.refuse_change_of_emptied_content();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER emptied_content_is_final;

-- Reading the occurrences is an event of the chain about the item, the step occurrences_read; only the list of the steps grows (the last one is the step 0041's), and every clause the chain fixes stays.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted', 'occurrences_read'));
