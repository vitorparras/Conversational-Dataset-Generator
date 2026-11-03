-- The load of a restore by the credential of the application (#64, CA-5). The digest of an event covers the moment and the one who recorded, which the server fills, and the application has no INSERT on them:
-- it could never put back an event with the digest it had. The functions below run as the owner and put the rows of a copy back as they were, in a database that has never held anything,
-- inside a window that opens once and closes for good. The proof of what was put back is outside: the tip noted apart from the copy (005).
CREATE TABLE public.restore_window (state text PRIMARY KEY CHECK (state IN ('opened', 'closed')), at timestamptz NOT NULL DEFAULT clock_timestamp(), approved boolean,
    CHECK ((state = 'closed') = (approved IS NOT NULL)));
REVOKE ALL ON public.restore_window FROM PUBLIC;
GRANT SELECT ON public.restore_window TO app;
REVOKE UPDATE, DELETE, TRUNCATE ON public.restore_window FROM CURRENT_USER;

CREATE FUNCTION public.restore_require_open() RETURNS void LANGUAGE plpgsql SET search_path = pg_catalog AS $require$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM public.restore_window WHERE state = 'opened') OR EXISTS (SELECT 1 FROM public.restore_window WHERE state = 'closed') THEN
        RAISE EXCEPTION 'the window of the load is not open' USING ERRCODE = 'CE005';
    END IF;
END
$require$;

-- Opens the window: once in the life of the database, and only while it has never held a domain, a person, a derivation label or an event.
CREATE FUNCTION public.restore_open() RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $open$
BEGIN
    IF EXISTS (SELECT 1 FROM public.restore_window) THEN
        RAISE EXCEPTION 'the window of the load opens once' USING ERRCODE = 'CE005';
    END IF;

    IF EXISTS (SELECT 1 FROM public.custody_event) OR EXISTS (SELECT 1 FROM public.person) OR EXISTS (SELECT 1 FROM public.domain) OR EXISTS (SELECT 1 FROM public.derivation_label) THEN
        RAISE EXCEPTION 'the database is not empty: a restore loads into one that has never held anything' USING ERRCODE = 'CE005';
    END IF;

    INSERT INTO public.restore_window (state) VALUES ('opened');
END
$open$;

-- Puts back the rows of one table, as the copy has them, ids and moments included. Content is loaded domain by domain, as the row policy asks; the people come back active, because an event
-- is written only by an active person, and the ones that were not are deactivated after the chain is back.
CREATE FUNCTION public.restore_load(table_name text, rows jsonb) RETURNS bigint LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $load$
DECLARE
    target regclass;
    declared integer;
    part bigint;
    loaded bigint := 0;
BEGIN
    PERFORM public.restore_require_open();
    IF table_name NOT IN ('public.domain', 'public.derivation_label', 'public.person', 'public.person_change', 'public.person_credential',
                          'content.item_text', 'content.reversal_map', 'content.item_emptied') THEN
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
        UPDATE public.person SET active = true WHERE NOT active;
    END IF;

    IF table_name NOT IN ('public.derivation_label', 'public.person_credential') AND loaded > 0 THEN
        PERFORM pg_catalog.setval(pg_catalog.pg_get_serial_sequence(table_name, 'id')::regclass, (SELECT max((r ->> 'id')::bigint) FROM jsonb_array_elements(rows) r));
    END IF;

    RETURN loaded;
END
$load$;

-- Puts back the events of the chain, in the order the array has them. Each is written by the same trigger that writes every event, which gives it the sequence and the digests from the tip,
-- and has to come out with the sequence and the digests the copy has: the first that does not stops the load, and nothing of the call stays.
CREATE FUNCTION public.restore_events(rows jsonb) RETURNS bigint LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $events$
DECLARE
    copied public.custody_event;
    stored public.custody_event;
    loaded bigint := 0;
BEGIN
    PERFORM public.restore_require_open();
    FOR copied IN SELECT r.* FROM jsonb_array_elements(rows) WITH ORDINALITY AS a(item, n), jsonb_populate_record(NULL::public.custody_event, a.item) r ORDER BY a.n LOOP
        INSERT INTO public.custody_event (occurred_at, actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, recorded_by, reference)
        VALUES (copied.occurred_at, copied.actor_person_id, copied.actor_system, copied.item_kind, copied.item_id, copied.step, copied.reason, copied.corrects, copied.recorded_by, copied.reference)
        RETURNING * INTO stored;
        IF stored.sequence IS DISTINCT FROM copied.sequence OR stored.previous_digest IS DISTINCT FROM copied.previous_digest OR stored.digest IS DISTINCT FROM copied.digest THEN
            RAISE EXCEPTION 'the event % does not come back as the copy has it', copied.sequence USING ERRCODE = 'CE005';
        END IF;

        loaded := loaded + 1;
    END LOOP;

    RETURN loaded;
END
$events$;

-- Closes the window for good, approved or not. When approved, what the database counts by itself is counted again from what was put back (the domains that still hold something).
CREATE FUNCTION public.restore_close(approved boolean) RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $close$
DECLARE declared integer;
BEGIN
    PERFORM public.restore_require_open();
    IF approved THEN
        DELETE FROM public.content_domain;
        FOR declared IN SELECT id FROM public.domain LOOP
            PERFORM pg_catalog.set_config('app.domain', declared::text, true);
            INSERT INTO public.content_domain SELECT declared, 'item_text', count(*) FROM content.item_text WHERE emptied_at IS NULL HAVING count(*) > 0;
            INSERT INTO public.content_domain SELECT declared, 'reversal_map', count(*) FROM content.reversal_map WHERE emptied_at IS NULL HAVING count(*) > 0;
        END LOOP;
    END IF;

    INSERT INTO public.restore_window (state, approved) VALUES ('closed', approved);
END
$close$;

REVOKE ALL ON FUNCTION public.restore_require_open(), public.restore_open(), public.restore_load(text, jsonb), public.restore_events(jsonb), public.restore_close(boolean) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.restore_open(), public.restore_load(text, jsonb), public.restore_events(jsonb), public.restore_close(boolean) TO app;
