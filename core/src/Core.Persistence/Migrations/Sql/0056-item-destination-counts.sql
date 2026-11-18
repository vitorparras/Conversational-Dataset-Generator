-- How many passages of an item go to each destination (issue 821): removed, pseudonymized and preserved, counted from the decided occurrences in the very insert of the item. They are numbers and
-- not a word of the material, kept in clear beside the item so the detail of a batch shows them without opening a thing; the items written before the step count none.
ALTER TABLE content.item_text
    ADD COLUMN removed_passages integer NOT NULL DEFAULT 0,
    ADD COLUMN pseudonymized_passages integer NOT NULL DEFAULT 0,
    ADD COLUMN preserved_passages integer NOT NULL DEFAULT 0,
    ADD CONSTRAINT item_text_destination_counts CHECK (removed_passages >= 0 AND pseudonymized_passages >= 0 AND preserved_passages >= 0);

-- An emptied item counts none, as it holds no occurrence: the shape of an emptied row (the step 0052) lists the counts too.
ALTER TABLE content.item_text
    DROP CONSTRAINT item_text_emptied,
    ADD CONSTRAINT item_text_emptied CHECK ((emptied_at IS NULL AND raw_text IS NOT NULL)
        OR (emptied_at IS NOT NULL AND raw_text IS NULL AND sanitized_text IS NULL AND pseudonymized_text IS NULL AND occurrences IS NULL
            AND removed_passages = 0 AND pseudonymized_passages = 0 AND preserved_passages = 0));

GRANT UPDATE (removed_passages, pseudonymized_passages, preserved_passages) ON content.item_text TO app;

-- A row not emptied keeps its counts whoever writes, as it keeps its texts and its occurrences: the trigger lets an update through only when it empties the row.
DROP TRIGGER emptied_content_is_final ON content.item_text;
CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL
        OR (NEW.emptied_at IS NULL
            AND (NEW.raw_text IS DISTINCT FROM OLD.raw_text OR NEW.sanitized_text IS DISTINCT FROM OLD.sanitized_text OR NEW.pseudonymized_text IS DISTINCT FROM OLD.pseudonymized_text
                OR NEW.occurrences IS DISTINCT FROM OLD.occurrences
                OR NEW.removed_passages IS DISTINCT FROM OLD.removed_passages OR NEW.pseudonymized_passages IS DISTINCT FROM OLD.pseudonymized_passages
                OR NEW.preserved_passages IS DISTINCT FROM OLD.preserved_passages)))
    EXECUTE FUNCTION public.refuse_change_of_emptied_content();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER emptied_content_is_final;
