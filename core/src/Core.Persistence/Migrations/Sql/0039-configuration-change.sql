-- A change of configuration submitted on the screen (issue 634, decision 0053): the key, the new value and the motive, a code of a closed list, by a person, at the moment of the server. A row is
-- never changed nor removed: the approval and the application are records of their own. The value is never a secret: the list of what the screen changes lives in the code and keeps them out.
--
-- The chain learns the submission: the item kind configuration_change (its id is the row's) and the step configuration_submitted, authored by the person who submitted, with the reason of an
-- administrative act and no reference. Only the checks change, from the last ones the earlier steps left (the item kinds and the reasons from the 0034, the steps from the 0036): everything they list
-- and every clause they fix stays, and the submission comes in beside it.
ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN
        ('person', 'item_text', 'reversal_map', 'custody_chain', 'content_epoch', 'domain', 'sign_in_attempt', 'master_key', 'ingestion_batch', 'configuration_change')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated',
         'sign_in_accepted', 'sign_in_refused', 'credential_recomputed', 'key_loss_declared', 'recovery_tested', 'verify_guarantees',
         'owner_window_opened', 'owner_window_closed', 'restored', 'restore_rehearsed', 'domain_label_revoked', 'domain_rewrapped',
         'batch_opened', 'batch_closed', 'password_changed', 'configuration_submitted')),
    ADD CONSTRAINT custody_event_configuration_shape CHECK (
        (step = 'configuration_submitted') = (item_kind = 'configuration_change')
        AND (step <> 'configuration_submitted' OR (reason = 'administrative_act' AND actor_person_id IS NOT NULL AND corrects IS NULL AND reference IS NULL)));

-- The key is a section and a name, as the loader reads them; the value is printable ASCII with a size; the motive is a code in snake_case, never free text.
CREATE TABLE public.configuration_change (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    config_key text NOT NULL CONSTRAINT configuration_change_key CHECK (config_key ~ '^[A-Z][A-Za-z]{0,62}:[A-Z][A-Za-z]{0,62}$'),
    new_value text NOT NULL CONSTRAINT configuration_change_value CHECK (char_length(new_value) BETWEEN 1 AND 256 AND new_value ~ '^[ -~]+$'),
    motive text NOT NULL CONSTRAINT configuration_change_motive CHECK (motive ~ '^[a-z][a-z_]{0,31}$'),
    submitted_by bigint NOT NULL CONSTRAINT configuration_change_author REFERENCES public.person (id),
    submitted_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

-- The application adds submissions and reads them; the moment is the server's. Nobody changes, deletes or empties a row, the owner included, also in a session set to replica.
GRANT SELECT ON public.configuration_change TO app;
GRANT INSERT (config_key, new_value, motive, submitted_by) ON public.configuration_change TO app;
REVOKE UPDATE, DELETE, TRUNCATE ON public.configuration_change FROM PUBLIC, CURRENT_USER;

CREATE FUNCTION public.refuse_configuration_change_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'public.configuration_change: % refused', TG_OP USING ERRCODE = 'CE001', SCHEMA = 'public', TABLE = 'configuration_change';
END
$refuse$;

-- The event of the submission is looked for when the transaction ends, because the row has no number until it is inserted: it must be about this row, by the person who submitted, in this transaction.
CREATE FUNCTION public.refuse_configuration_change_without_event() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF NOT public.restore_window_is_open() AND NOT EXISTS (
        SELECT 1 FROM public.custody_event e
        WHERE e.item_kind = 'configuration_change' AND e.item_id = NEW.id AND e.step = 'configuration_submitted' AND e.actor_person_id = NEW.submitted_by
          AND e.xmin = pg_catalog.pg_current_xact_id()::pg_catalog.xid
          AND e.occurred_at >= pg_catalog.transaction_timestamp()) THEN
        RAISE EXCEPTION 'a change of configuration is submitted only with the event of its submission, by the same person, in the same transaction'
            USING ERRCODE = 'CE005', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
    END IF;

    RETURN NULL;
END
$refuse$;

CREATE TRIGGER configuration_change_never_changed BEFORE UPDATE OR DELETE ON public.configuration_change FOR EACH ROW EXECUTE FUNCTION public.refuse_configuration_change_change();
CREATE TRIGGER configuration_change_never_emptied BEFORE TRUNCATE ON public.configuration_change FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_configuration_change_change();
CREATE CONSTRAINT TRIGGER configuration_change_needs_event AFTER INSERT ON public.configuration_change DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
    EXECUTE FUNCTION public.refuse_configuration_change_without_event();
ALTER TABLE public.configuration_change ENABLE ALWAYS TRIGGER configuration_change_never_changed;
ALTER TABLE public.configuration_change ENABLE ALWAYS TRIGGER configuration_change_never_emptied;
ALTER TABLE public.configuration_change ENABLE ALWAYS TRIGGER configuration_change_needs_event;

-- The load of a restore puts the submissions back too (the 0034 as it was, with this table among the ones it accepts, after the people it points at).
CREATE OR REPLACE FUNCTION public.restore_load(table_name text, rows jsonb) RETURNS bigint LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $load$
DECLARE
    target regclass;
    declared integer;
    part bigint;
    loaded bigint := 0;
BEGIN
    PERFORM public.restore_require_open();
    BEGIN
        IF table_name NOT IN ('public.domain', 'public.derivation_label', 'public.derivation_label_revocation', 'public.person', 'public.person_change', 'public.person_credential',
                              'public.sign_in_attempt', 'public.configuration_change', 'content.ingestion_batch', 'content.item_text', 'content.reversal_map', 'content.item_emptied') THEN
            RAISE EXCEPTION 'the table is not one the load puts back' USING ERRCODE = 'CE005';
        END IF;

        target := table_name::regclass;
        IF table_name LIKE 'content.%' THEN
            FOR declared IN SELECT DISTINCT (r ->> 'domain_id')::integer FROM jsonb_array_elements(rows) r LOOP
                PERFORM pg_catalog.set_config('app.domain', declared::text, true);
                EXECUTE format('INSERT INTO %s OVERRIDING SYSTEM VALUE SELECT * FROM jsonb_populate_recordset(NULL::%s, $1) WHERE domain_id = $2', target, target) USING rows, declared;
                GET DIAGNOSTICS part = ROW_COUNT;
                loaded := loaded + part;
            END LOOP;
        ELSE
            EXECUTE format('INSERT INTO %s OVERRIDING SYSTEM VALUE SELECT * FROM jsonb_populate_recordset(NULL::%s, $1)', target, target) USING rows;
            GET DIAGNOSTICS loaded = ROW_COUNT;
        END IF;

        IF table_name = 'public.person' THEN
            INSERT INTO public.restore_inactive SELECT id FROM public.person WHERE NOT active;
            UPDATE public.person SET active = true WHERE NOT active;
        END IF;

        IF table_name NOT IN ('public.derivation_label', 'public.derivation_label_revocation', 'public.person_credential') AND loaded > 0 THEN
            PERFORM pg_catalog.setval(pg_catalog.pg_get_serial_sequence(table_name, 'id')::regclass, (SELECT max((r ->> 'id')::bigint) FROM jsonb_array_elements(rows) r));
        END IF;
    EXCEPTION WHEN OTHERS THEN
        PERFORM public.restore_close(false);
        RETURN -1;
    END;

    RETURN loaded;
END
$load$;
