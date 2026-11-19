-- What the sanitization left with each item (issues 87, 92, 93 and 98): the version of the list of steps that produced the sanitized text, the trail that translates its positions back to the raw
-- text, sealed with the purpose of the raw text as the sanitized one is, and how many edits each step made, in the order of the steps, as whole numbers and never a position. The four come together or
-- not at all: no sanitized text without its version, its trail and its counts; the items written before the step have none of them.
ALTER TABLE content.item_text
    ADD COLUMN sanitization_version text,
    ADD COLUMN sanitization_trail bytea,
    ADD COLUMN sanitization_counts integer[],
    ADD CONSTRAINT item_text_sanitization_version FOREIGN KEY (sanitization_version, domain_id) REFERENCES content.sanitization_version (version, domain_id),
    ADD CONSTRAINT item_text_sanitization_together CHECK ((sanitized_text IS NULL) = (sanitization_version IS NULL)
        AND (sanitized_text IS NULL) = (sanitization_trail IS NULL)
        AND (sanitized_text IS NULL) = (sanitization_counts IS NULL)),
    ADD CONSTRAINT item_text_sanitization_counts CHECK (sanitization_counts IS NULL
        OR (pg_catalog.array_position(sanitization_counts, NULL) IS NULL AND 0 <= ALL (sanitization_counts)));

COMMENT ON COLUMN content.item_text.sanitization_trail IS 'protected:texto-bruto';

-- One count for each step of the version, no more and no fewer, refused as a check (23514): not as the refusal of a batch not open (CE006), which the writer reads as such.
-- The check runs after the insert, so that a version written by the same statement, ahead of the item, is already seen.
CREATE FUNCTION public.refuse_counts_not_matching_steps() RETURNS trigger
LANGUAGE plpgsql
AS $counts$
BEGIN
    IF pg_catalog.cardinality(NEW.sanitization_counts) IS DISTINCT FROM (
        SELECT pg_catalog.array_length(pg_catalog.string_to_array(v.steps, E'\n'), 1)
        FROM content.sanitization_version v WHERE v.version = NEW.sanitization_version AND v.domain_id = NEW.domain_id) THEN
        RAISE EXCEPTION 'the counts of the sanitization are one for each step of its version'
            USING ERRCODE = '23514', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NULL;
END
$counts$;

CREATE TRIGGER sanitization_counts_match_steps AFTER INSERT ON content.item_text
    FOR EACH ROW WHEN (NEW.sanitization_version IS NOT NULL) EXECUTE FUNCTION public.refuse_counts_not_matching_steps();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER sanitization_counts_match_steps;

-- An emptied item holds no sanitization: the shape of an emptied row (the step 0056) lists the three columns too, and the emptying clears them.
ALTER TABLE content.item_text
    DROP CONSTRAINT item_text_emptied,
    ADD CONSTRAINT item_text_emptied CHECK ((emptied_at IS NULL AND raw_text IS NOT NULL)
        OR (emptied_at IS NOT NULL AND raw_text IS NULL AND sanitized_text IS NULL AND pseudonymized_text IS NULL AND occurrences IS NULL
            AND removed_passages = 0 AND pseudonymized_passages = 0 AND preserved_passages = 0
            AND sanitization_version IS NULL AND sanitization_trail IS NULL AND sanitization_counts IS NULL));

GRANT UPDATE (sanitization_version, sanitization_trail, sanitization_counts) ON content.item_text TO app;

-- A row not emptied keeps its sanitization whoever writes, as it keeps its texts: the trigger lets an update through only when it empties the row.
DROP TRIGGER emptied_content_is_final ON content.item_text;
CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL
        OR (NEW.emptied_at IS NULL
            AND (NEW.raw_text IS DISTINCT FROM OLD.raw_text OR NEW.sanitized_text IS DISTINCT FROM OLD.sanitized_text OR NEW.pseudonymized_text IS DISTINCT FROM OLD.pseudonymized_text
                OR NEW.occurrences IS DISTINCT FROM OLD.occurrences
                OR NEW.removed_passages IS DISTINCT FROM OLD.removed_passages OR NEW.pseudonymized_passages IS DISTINCT FROM OLD.pseudonymized_passages
                OR NEW.preserved_passages IS DISTINCT FROM OLD.preserved_passages
                OR NEW.sanitization_version IS DISTINCT FROM OLD.sanitization_version OR NEW.sanitization_trail IS DISTINCT FROM OLD.sanitization_trail
                OR NEW.sanitization_counts IS DISTINCT FROM OLD.sanitization_counts)))
    EXECUTE FUNCTION public.refuse_change_of_emptied_content();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER emptied_content_is_final;
