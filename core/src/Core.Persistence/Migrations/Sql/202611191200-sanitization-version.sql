-- The versions of the list of sanitization steps (issue 87, decision 0015): each version is written once per domain, whatever the number of batches that used it, and never changes. The version is the
-- SHA-256 of the steps, one name@version per line, in the order they ran; the database checks the fingerprint, so a version cannot name a list it does not hash. Names of steps and their versions are
-- not material: the table holds no word of the content, and every session may read it.
SELECT public.create_content_table('sanitization_version',
    $columns$version text NOT NULL,
     steps text NOT NULL,
     CONSTRAINT sanitization_version_unique UNIQUE (version, domain_id),
     CONSTRAINT sanitization_version_form CHECK (version ~ '^[0-9a-f]{64}$'),
     CONSTRAINT sanitization_version_steps CHECK (steps ~ '^[a-z][a-z0-9_]{0,62}@[0-9]+(\n[a-z][a-z0-9_]{0,62}@[0-9]+)*$'),
     CONSTRAINT sanitization_version_fingerprint CHECK (pg_catalog.encode(pg_catalog.sha256(pg_catalog.convert_to(steps, 'UTF8')), 'hex') = version)$columns$);

GRANT SELECT ON content.sanitization_version TO PUBLIC;
GRANT INSERT (domain_id, version, steps) ON content.sanitization_version TO app;

-- Written once: no UPDATE, no DELETE, by any session, and no TRUNCATE. The refusal carries the SQLSTATE of the other refusals of the content (CE006) and names the table and no value.
CREATE FUNCTION public.refuse_sanitization_version_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'a version of the sanitization steps is written once and never changes'
        USING ERRCODE = 'CE006', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
END
$refuse$;

CREATE TRIGGER sanitization_version_is_written_once BEFORE UPDATE OR DELETE ON content.sanitization_version FOR EACH ROW EXECUTE FUNCTION public.refuse_sanitization_version_change();
CREATE TRIGGER sanitization_version_is_never_truncated BEFORE TRUNCATE ON content.sanitization_version FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_sanitization_version_change();
ALTER TABLE content.sanitization_version ENABLE ALWAYS TRIGGER sanitization_version_is_written_once;
ALTER TABLE content.sanitization_version ENABLE ALWAYS TRIGGER sanitization_version_is_never_truncated;

-- The load of a restore puts the versions back, before the items that point to them (the 0046 as it was, with the table among the ones it accepts).
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
                              'public.sign_in_attempt', 'public.configuration_change', 'content.ingestion_batch', 'content.batch_origin', 'content.sanitization_version', 'content.item_text',
                              'content.reversal_map', 'content.item_emptied', 'content.replication_occurrence', 'content.replication_mark') THEN
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
