-- The chain of custody: one event after another, each carrying the digest of the one before and its own, so that changing or removing one in the middle breaks the link of the next.
-- One chain for the installation. The server fills the sequence, the moment and both digests, so that the application supplies none of them and cannot supply a wrong one.

-- The single place that says how an event is written down before it is digested: every field in a fixed order, a text with its length in front and an absent number marked as absent,
-- so that two different events never encode alike. The verification of the chain digests with this same function.
CREATE FUNCTION public.custody_digest(
    previous bytea, sequence bigint, occurred_at timestamptz, actor_person_id bigint, actor_system boolean,
    item_kind text, item_id bigint, step text, reason text, corrects bigint, recorded_by text)
RETURNS bytea
LANGUAGE plpgsql
IMMUTABLE
AS $$
BEGIN
    IF previous IS NULL OR pg_catalog.octet_length(previous) <> 32 OR sequence IS NULL OR occurred_at IS NULL OR actor_system IS NULL
        OR item_kind IS NULL OR item_id IS NULL OR step IS NULL OR reason IS NULL OR recorded_by IS NULL THEN
        RAISE EXCEPTION 'custody_digest: the input is outside the contract' USING ERRCODE = 'invalid_parameter_value';
    END IF;

    RETURN pg_catalog.sha256(
        previous
        || pg_catalog.int8send(sequence)
        || pg_catalog.timestamptz_send(occurred_at)
        || CASE WHEN actor_person_id IS NULL THEN '\x00'::bytea ELSE '\x01'::bytea || pg_catalog.int8send(actor_person_id) END
        || CASE WHEN actor_system THEN '\x01'::bytea ELSE '\x00'::bytea END
        || pg_catalog.int4send(pg_catalog.octet_length(pg_catalog.convert_to(item_kind, 'UTF8'))) || pg_catalog.convert_to(item_kind, 'UTF8')
        || pg_catalog.int8send(item_id)
        || pg_catalog.int4send(pg_catalog.octet_length(pg_catalog.convert_to(step, 'UTF8'))) || pg_catalog.convert_to(step, 'UTF8')
        || pg_catalog.int4send(pg_catalog.octet_length(pg_catalog.convert_to(reason, 'UTF8'))) || pg_catalog.convert_to(reason, 'UTF8')
        || CASE WHEN corrects IS NULL THEN '\x00'::bytea ELSE '\x01'::bytea || pg_catalog.int8send(corrects) END
        || pg_catalog.int4send(pg_catalog.octet_length(pg_catalog.convert_to(recorded_by, 'UTF8'))) || pg_catalog.convert_to(recorded_by, 'UTF8'));
END
$$;

-- What the first event points to: thirty-two zero bytes, the length of a digest.
CREATE FUNCTION public.custody_origin() RETURNS bytea LANGUAGE sql IMMUTABLE AS $$ SELECT pg_catalog.decode(pg_catalog.repeat('00', 32), 'hex') $$;

CREATE TABLE public.custody_event (
    sequence bigint PRIMARY KEY,
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    actor_person_id bigint CONSTRAINT custody_event_actor_fkey REFERENCES public.person (id),
    actor_system boolean NOT NULL DEFAULT false,
    -- Item, step and reason come from lists the code declares (CustodyNames), and the database holds the same lists: no name of a person and no piece of material gets into a record nobody removes.
    item_kind text NOT NULL CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN ('person')),
    item_id bigint NOT NULL,
    step text NOT NULL CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction')),
    reason text NOT NULL CONSTRAINT custody_event_reason_listed CHECK (reason IN ('administrative_act', 'correction_of_record')),
    -- The event this one corrects. The trigger checks that it exists: a key to the table itself would need the right to update it, which nobody has.
    corrects bigint,
    previous_digest bytea NOT NULL UNIQUE CHECK (octet_length(previous_digest) = 32),
    digest bytea NOT NULL UNIQUE CHECK (octet_length(digest) = 32),
    recorded_by text NOT NULL DEFAULT session_user,
    -- Nobody is left blank: the author is a person or the system itself, never both and never neither.
    CONSTRAINT custody_event_author CHECK ((actor_person_id IS NOT NULL) <> actor_system)
);

-- Links the event to the tip of the chain and digests it. Two writers that read the same tip compute the same sequence and the same previous digest, and the key and the unique constraint
-- refuse the second whatever the isolation of its transaction. The author, when a person, must be active at the moment of the writing, and an event that corrects must name one that exists.
CREATE FUNCTION public.custody_event_link() RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    tip_sequence bigint;
    tip_digest bytea;
    author_active boolean;
BEGIN
    IF NEW.actor_person_id IS NOT NULL THEN
        SELECT p.active INTO author_active FROM public.person p WHERE p.id = NEW.actor_person_id;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'the author of a custody event is not a registered person'
                USING ERRCODE = 'foreign_key_violation', CONSTRAINT = 'custody_event_actor_fkey';
        ELSIF NOT author_active THEN
            RAISE EXCEPTION 'the author of a custody event must be an active person'
                USING ERRCODE = 'check_violation', CONSTRAINT = 'custody_event_actor_active';
        END IF;
    END IF;

    IF NEW.corrects IS NOT NULL AND NOT EXISTS (SELECT 1 FROM public.custody_event c WHERE c.sequence = NEW.corrects) THEN
        RAISE EXCEPTION 'the event to correct is not in the chain'
            USING ERRCODE = 'foreign_key_violation', CONSTRAINT = 'custody_event_corrects_fkey';
    END IF;

    SELECT e.sequence, e.digest INTO tip_sequence, tip_digest FROM public.custody_event e ORDER BY e.sequence DESC LIMIT 1;
    NEW.sequence := COALESCE(tip_sequence, 0) + 1;
    NEW.previous_digest := COALESCE(tip_digest, public.custody_origin());
    NEW.digest := public.custody_digest(
        NEW.previous_digest, NEW.sequence, NEW.occurred_at, NEW.actor_person_id, NEW.actor_system,
        NEW.item_kind, NEW.item_id, NEW.step, NEW.reason, NEW.corrects, NEW.recorded_by);
    RETURN NEW;
END
$$;

CREATE TRIGGER custody_event_link BEFORE INSERT ON public.custody_event FOR EACH ROW EXECUTE FUNCTION public.custody_event_link();

-- The application adds events and reads the chain, naming only what it knows: the digests, the sequence, the moment and who recorded are the server's. Nobody changes, deletes or empties it, the owner included.
GRANT SELECT ON public.custody_event TO PUBLIC;
GRANT INSERT (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects) ON public.custody_event TO PUBLIC;
REVOKE UPDATE, DELETE, TRUNCATE ON public.custody_event FROM PUBLIC, CURRENT_USER;
