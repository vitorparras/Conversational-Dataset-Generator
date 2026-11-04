-- Every attempt to sign in, accepted or refused, is a row of sign_in_attempt and an event of the chain written in the same transaction (issue 71, part 2): the step sign_in_accepted or
-- sign_in_refused, about the attempt (item kind sign_in_attempt, its id is the row's). The row holds where the attempt came from, as the network prefix (/24 of IPv4, /48 of IPv6, never the whole address,
-- NULL when the origin is unknown) and the moment. An accepted attempt and a refusal for a wrong password are authored by the person; a name nobody goes by, by the system: the name typed is nowhere
-- and the password, or anything derived from it, never gets here. The answer refused for the limit of answers that wait is not recorded: it is given without reaching the database, which is what the limit is for. The recomputation of a credential at sign-in is the step credential_recomputed,
-- about the person who signed in and authored by the same person.
CREATE TABLE public.sign_in_attempt (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    network cidr CONSTRAINT sign_in_attempt_network_prefix CHECK (network IS NULL OR (family(network) = 4 AND masklen(network) = 24) OR (family(network) = 6 AND masklen(network) = 48)),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE INDEX sign_in_attempt_series_idx ON public.sign_in_attempt (occurred_at, network);

-- The application adds attempts and reads them for the series; the moment is the server's. Nobody changes, deletes or empties a row, as with the chain (0009), and the refusal also holds for the owner
-- and in a session that set session_replication_role to replica.
GRANT SELECT ON public.sign_in_attempt TO app;
GRANT INSERT (network) ON public.sign_in_attempt TO app;
REVOKE UPDATE, DELETE, TRUNCATE ON public.sign_in_attempt FROM PUBLIC, CURRENT_USER;

CREATE FUNCTION public.refuse_sign_in_attempt_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'public.sign_in_attempt: % refused', TG_OP USING ERRCODE = 'CE001', SCHEMA = 'public', TABLE = 'sign_in_attempt';
END
$refuse$;

CREATE TRIGGER sign_in_attempt_never_changed BEFORE UPDATE OR DELETE ON public.sign_in_attempt FOR EACH ROW EXECUTE FUNCTION public.refuse_sign_in_attempt_change();
CREATE TRIGGER sign_in_attempt_never_emptied BEFORE TRUNCATE ON public.sign_in_attempt FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_sign_in_attempt_change();
ALTER TABLE public.sign_in_attempt ENABLE ALWAYS TRIGGER sign_in_attempt_never_changed;
ALTER TABLE public.sign_in_attempt ENABLE ALWAYS TRIGGER sign_in_attempt_never_emptied;

ALTER TABLE public.custody_event
    DROP CONSTRAINT custody_event_item_kind_listed,
    DROP CONSTRAINT custody_event_step_listed,
    DROP CONSTRAINT custody_event_reason_listed,
    ADD CONSTRAINT custody_event_item_kind_listed CHECK (item_kind IN ('person', 'item_text', 'reversal_map', 'custody_chain', 'content_epoch', 'domain', 'sign_in_attempt')),
    ADD CONSTRAINT custody_event_step_listed CHECK (step IN
        ('person_created', 'person_deactivated', 'person_function_changed', 'person_password_set', 'correction',
         'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'verify_chain', 'item_emptied', 'item_emptying_refused', 'epoch_rotated',
         'domain_created', 'seal_recorded', 'domain_label_rotated', 'sign_in_accepted', 'sign_in_refused', 'credential_recomputed')),
    ADD CONSTRAINT custody_event_reason_listed CHECK (reason IN
        ('administrative_act', 'correction_of_record', 'content_read', 'verification_passed', 'verification_failed',
         'removal_requested_by_subject', 'contract_requirement', 'author_without_function', 'item_already_emptied',
         'credentials_accepted', 'password_wrong', 'account_not_recognized')),
    ADD CONSTRAINT custody_event_sign_in_shape CHECK (
        (step IN ('sign_in_accepted', 'sign_in_refused')) = (item_kind = 'sign_in_attempt')
        AND (step = 'sign_in_accepted') = (reason = 'credentials_accepted')
        AND (step = 'sign_in_refused') = (reason IN ('password_wrong', 'account_not_recognized'))
        AND (step NOT IN ('sign_in_accepted', 'sign_in_refused') OR (corrects IS NULL AND reference IS NULL AND (actor_person_id IS NOT NULL) = (reason IN ('credentials_accepted', 'password_wrong'))))
        AND (step <> 'credential_recomputed'
             OR (item_kind = 'person' AND coalesce(actor_person_id = item_id, false) AND reason = 'administrative_act' AND corrects IS NULL AND reference IS NULL)));

-- The link of the step 0016 as it was, with the three steps of the entry added to the ones a chain that failed its last verification still takes: without them nobody could sign in to read or to verify.
CREATE OR REPLACE FUNCTION public.custody_event_link() RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    tip_sequence bigint;
    tip_digest bytea;
    author_active boolean;
BEGIN
    IF NEW.step = 'verify_chain' AND NEW.reason = 'verification_passed' AND public.custody_chain_broken() THEN
        RAISE EXCEPTION 'the custody chain failed its last verification: a verdict that passed is not added until the chain is restored'
            USING ERRCODE = 'CE003', SCHEMA = 'public', TABLE = 'custody_event';
    END IF;

    IF NEW.step NOT IN ('verify_chain', 'raw_text_read', 'sanitized_text_read', 'reversal_entries_read', 'sign_in_accepted', 'sign_in_refused', 'credential_recomputed')
        AND public.custody_chain_broken() THEN
        RAISE EXCEPTION 'the custody chain failed its last verification: only a verification, a reading or an entry is added until the chain is restored'
            USING ERRCODE = 'CE002', SCHEMA = 'public', TABLE = 'custody_event';
    END IF;

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
        NEW.item_kind, NEW.item_id, NEW.step, NEW.reason, NEW.corrects, NEW.recorded_by, NEW.reference);
    RETURN NEW;
END
$$;
