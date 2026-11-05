-- The load of a restore puts back the revocations of the labels (issue 647): without them, the restored database would encrypt new content under a label the owner had revoked.
-- Only restore_load changes, from the last definition in force (the 0027): the same body, with derivation_label_revocation among the tables it accepts, and no serial to set for it, since
-- it has none. The foreign key to derivation_label refuses a revocation of a label the copy does not hold, which closes the window as failed, as any row the table refuses does.
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
                              'public.sign_in_attempt', 'content.item_text', 'content.reversal_map', 'content.item_emptied') THEN
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
