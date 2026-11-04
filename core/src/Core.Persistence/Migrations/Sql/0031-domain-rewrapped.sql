-- The rewrap of a domain (issue 56, part 2) wraps a key again in batches that each commit alone, and the epoch does not change, so the event of the rotation of an epoch is not the one to ask
-- (decision 0050): the step domain_rewrapped, about the domain and written in the same transaction as the batch, releases a change of the wrapped key that keeps the epoch.
CREATE OR REPLACE FUNCTION public.refuse_item_change_without_event() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.emptied_at IS NOT NULL THEN
        IF NOT public.custody_event_of_this_transaction('item_text', NEW.id, 'item_emptied') THEN
            RAISE EXCEPTION 'an item is emptied only with the event of its emptying in the same transaction'
                USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
        END IF;
    ELSIF NOT (public.custody_event_of_this_transaction('content_epoch', NEW.dek_epoch, 'epoch_rotated')
        OR (NEW.dek_epoch = OLD.dek_epoch AND public.custody_event_of_this_transaction('domain', NEW.domain_id, 'domain_rewrapped'))) THEN
        RAISE EXCEPTION 'a key is wrapped again only with the event of the rotation or of the rewrap in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;
    RETURN NEW;
END
$refuse$;
CREATE OR REPLACE FUNCTION public.refuse_map_change_without_event() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NEW.emptied_at IS NOT NULL THEN
        IF NOT public.custody_event_of_this_transaction('item_text', NEW.item_id, 'item_emptied') THEN
            RAISE EXCEPTION 'a map is emptied only with the event of the emptying of its item in the same transaction'
                USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
        END IF;
    ELSIF NOT (public.custody_event_of_this_transaction('content_epoch', NEW.dek_epoch, 'epoch_rotated')
        OR (NEW.dek_epoch = OLD.dek_epoch AND public.custody_event_of_this_transaction('domain', NEW.domain_id, 'domain_rewrapped'))) THEN
        RAISE EXCEPTION 'a key is wrapped again only with the event of the rotation or of the rewrap in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;
    RETURN NEW;
END
$refuse$;

ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_domain_shape,
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'domain_label_revoked', 'domain_rewrapped')),
    ADD CONSTRAINT custody_event_domain_shape CHECK (
        (step IN ('domain_created', 'domain_label_rotated', 'domain_label_revoked', 'domain_rewrapped')) = (item_kind = 'domain')
        AND (step NOT IN ('domain_created', 'domain_label_rotated', 'domain_label_revoked', 'domain_rewrapped')
            OR (actor_system AND reason = 'administrative_act' AND corrects IS NULL))
        AND (step NOT IN ('domain_created', 'domain_rewrapped') OR reference IS NULL)
        AND (step <> 'domain_label_rotated' OR coalesce(reference ~ '^v[1-9][0-9]*$', false))
        AND (step <> 'domain_label_revoked' OR coalesce(reference ~ '^(texto-bruto|mapa-reversao)/[1-9][0-9]*/v[1-9][0-9]*$', false))
        AND (step <> 'domain_label_revoked' OR split_part(reference, '/', 2) = item_id::text));
