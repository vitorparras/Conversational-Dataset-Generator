-- The shift of the dates of a domain (issue 108, decision 0020): one row for each domain, drawn once, sealed with the purpose deslocamento-datas under a key of its own wrapped under the
-- epoch, as the origin of a batch is (0042), and when it was drawn. The unique domain makes a second draw lose to the first, which it then reads. The shift is not unique among the
-- domains: each is drawn on its own, and knowing the others would help guess it.
SELECT public.create_content_table('date_shift',
    $columns$drawn_at timestamptz NOT NULL DEFAULT now(),
     shift bytea NOT NULL,
     dek_epoch integer NOT NULL,
     dek_wrapped bytea NOT NULL,
     CONSTRAINT date_shift_once UNIQUE (domain_id),
     CONSTRAINT date_shift_key CHECK (dek_epoch > 0)$columns$);

COMMENT ON COLUMN content.date_shift.shift IS 'protected:deslocamento-datas';
COMMENT ON COLUMN content.date_shift.dek_wrapped IS 'protected:deslocamento-datas';

GRANT SELECT, INSERT ON content.date_shift TO PUBLIC;
GRANT UPDATE (dek_epoch, dek_wrapped) ON content.date_shift TO app;

-- The shift never changes: only the key that wraps it is wrapped again, and only with the event of the rotation or of the rewrap in the same transaction, as for an origin.
CREATE FUNCTION public.refuse_date_shift_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.id IS DISTINCT FROM OLD.id OR NEW.domain_id IS DISTINCT FROM OLD.domain_id OR NEW.drawn_at IS DISTINCT FROM OLD.drawn_at OR NEW.shift IS DISTINCT FROM OLD.shift THEN
        RAISE EXCEPTION 'the shift of the dates of a domain never changes: only the key that wraps it is wrapped again'
            USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    IF NOT (public.custody_event_of_this_transaction('content_epoch', NEW.dek_epoch, 'epoch_rotated')
        OR (NEW.dek_epoch = OLD.dek_epoch AND public.custody_event_of_this_transaction('domain', NEW.domain_id, 'domain_rewrapped'))) THEN
        RAISE EXCEPTION 'a key is wrapped again only with the event of the rotation or of the rewrap in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NEW;
END
$refuse$;

CREATE FUNCTION public.refuse_date_shift_removal() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'the shift of the dates of a domain is never removed: drawing another would put what was exported under another shift'
        USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
END
$refuse$;

CREATE TRIGGER date_shift_change_is_bounded BEFORE UPDATE ON content.date_shift FOR EACH ROW EXECUTE FUNCTION public.refuse_date_shift_change();
CREATE TRIGGER date_shift_is_never_removed BEFORE DELETE ON content.date_shift FOR EACH ROW EXECUTE FUNCTION public.refuse_date_shift_removal();
CREATE TRIGGER date_shift_is_never_emptied BEFORE TRUNCATE ON content.date_shift FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_date_shift_removal();
ALTER TABLE content.date_shift ENABLE ALWAYS TRIGGER date_shift_change_is_bounded;
ALTER TABLE content.date_shift ENABLE ALWAYS TRIGGER date_shift_is_never_removed;
ALTER TABLE content.date_shift ENABLE ALWAYS TRIGGER date_shift_is_never_emptied;

-- The purpose is a third one of every domain (0013): the loss of its label may be declared and the label revoked as those of the other two are. The two checks that name the
-- purposes are made again from the last ones that fixed them (the shape of the loss of a key from the 0033, the shape of a domain from the 0031), with the new name beside the
-- others and every other clause as it was.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_key_loss_shape,
    DROP CONSTRAINT custody_event_domain_shape,
    ADD CONSTRAINT custody_event_key_loss_shape CHECK (
        (step IN ('key_loss_declared', 'recovery_tested')) = (item_kind = 'master_key')
        AND (step <> 'key_loss_declared' OR (
            actor_system AND reason = 'administrative_act' AND corrects IS NULL AND item_id BETWEEN 1 AND 7
            AND coalesce(reference ~ '^(master|(texto-bruto|mapa-reversao|deslocamento-datas)/[1-9][0-9]*/v[1-9][0-9]*)$', false)))
        AND (step <> 'recovery_tested' OR (
            actor_system AND reason = 'administrative_act' AND corrects IS NULL AND item_id >= 1
            AND (reference IS NULL OR reference ~ '^[0-9a-f]{64}$')))),
    ADD CONSTRAINT custody_event_domain_shape CHECK (
        (step IN ('domain_created', 'domain_label_rotated', 'domain_label_revoked', 'domain_rewrapped')) = (item_kind = 'domain')
        AND (step NOT IN ('domain_created', 'domain_label_rotated', 'domain_label_revoked', 'domain_rewrapped')
            OR (actor_system AND reason = 'administrative_act' AND corrects IS NULL))
        AND (step NOT IN ('domain_created', 'domain_rewrapped') OR reference IS NULL)
        AND (step <> 'domain_label_rotated' OR coalesce(reference ~ '^v[1-9][0-9]*$', false))
        AND (step <> 'domain_label_revoked' OR coalesce(reference ~ '^(texto-bruto|mapa-reversao|deslocamento-datas)/[1-9][0-9]*/v[1-9][0-9]*$', false))
        AND (step <> 'domain_label_revoked' OR split_part(reference, '/', 2) = item_id::text));
