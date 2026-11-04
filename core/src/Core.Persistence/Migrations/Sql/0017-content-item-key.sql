-- Each row that holds protected content carries a key of its own (decision 0049): a random key that seals the sealed columns of the row, kept here wrapped under a key made of the key of the
-- domain and the purpose and of the epoch named by dek_epoch, a secret that lives in a custody file and not in the database. Emptying the row destroys the wrapped key with the content.
-- Content stored before the keys cannot be given one without a second way to read it, so the step refuses to apply while a row not emptied exists, in any domain; the demonstration recreates the database.
DO $guard$
DECLARE
    declared integer;
BEGIN
    FOR declared IN SELECT id FROM public.domain LOOP
        PERFORM set_config('app.domain', declared::text, true);
        IF EXISTS (SELECT 1 FROM content.item_text WHERE emptied_at IS NULL) OR EXISTS (SELECT 1 FROM content.reversal_map WHERE emptied_at IS NULL) THEN
            RAISE EXCEPTION 'content that is not emptied exists without a key of its own: the step 0017 does not apply over it (decision 0049)';
        END IF;
    END LOOP;

    PERFORM set_config('app.domain', '', true);
END
$guard$;

ALTER TABLE content.item_text
    ADD COLUMN dek_epoch integer,
    ADD COLUMN dek_wrapped bytea,
    ADD CONSTRAINT item_text_key CHECK ((emptied_at IS NULL AND dek_wrapped IS NOT NULL AND dek_epoch > 0)
        OR (emptied_at IS NOT NULL AND dek_wrapped IS NULL AND dek_epoch IS NULL));

ALTER TABLE content.reversal_map
    ADD COLUMN dek_epoch integer,
    ADD COLUMN dek_wrapped bytea,
    ADD CONSTRAINT reversal_map_key CHECK ((emptied_at IS NULL AND dek_wrapped IS NOT NULL AND dek_epoch > 0)
        OR (emptied_at IS NOT NULL AND dek_wrapped IS NULL AND dek_epoch IS NULL));

COMMENT ON COLUMN content.item_text.dek_wrapped IS 'protected:texto-bruto';
COMMENT ON COLUMN content.reversal_map.dek_wrapped IS 'protected:mapa-reversao';

-- The application clears the keys with the content, and wraps them again under another epoch (the rotation of the epoch) without touching the content.
GRANT UPDATE (dek_epoch, dek_wrapped) ON content.item_text, content.reversal_map TO app;

-- The one change of content is still the emptying; what the keys add is that a row not emptied may change its wrapped key and the epoch of it, with the content as it was.
DROP TRIGGER emptied_content_is_final ON content.item_text;
DROP TRIGGER emptied_content_is_final ON content.reversal_map;
CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.item_text
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL
        OR (NEW.emptied_at IS NULL
            AND (NEW.raw_text IS DISTINCT FROM OLD.raw_text OR NEW.sanitized_text IS DISTINCT FROM OLD.sanitized_text OR NEW.pseudonymized_text IS DISTINCT FROM OLD.pseudonymized_text)))
    EXECUTE FUNCTION public.refuse_change_of_emptied_content();
CREATE TRIGGER emptied_content_is_final BEFORE UPDATE ON content.reversal_map
    FOR EACH ROW WHEN (OLD.emptied_at IS NOT NULL OR (NEW.emptied_at IS NULL AND NEW.entries IS DISTINCT FROM OLD.entries))
    EXECUTE FUNCTION public.refuse_change_of_emptied_content();
ALTER TABLE content.item_text ENABLE ALWAYS TRIGGER emptied_content_is_final;
ALTER TABLE content.reversal_map ENABLE ALWAYS TRIGGER emptied_content_is_final;
