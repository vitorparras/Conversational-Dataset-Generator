-- The notices of a person (issue 595): what the system tells someone without any message leaving it. A row holds a code from a closed list and, for a password set again, the number of sessions the act ended;
-- nothing free is written. Reading a notice marks it once and the row stays: nobody deletes or empties it, the owner included. A notice is not an event of the chain, so the copy of the data and a restoration leave it out.
CREATE TABLE public.person_notice (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    person_id bigint NOT NULL REFERENCES public.person (id),
    code text NOT NULL CONSTRAINT person_notice_code_listed CHECK (code IN ('password_reset', 'domain_read_only')),
    sessions_ended integer CONSTRAINT person_notice_sessions_range CHECK (sessions_ended BETWEEN 0 AND 999999),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    read_at timestamptz,
    CONSTRAINT person_notice_shape CHECK ((code = 'password_reset') = (sessions_ended IS NOT NULL)),
    CONSTRAINT person_notice_read_after_creation CHECK (read_at IS NULL OR read_at >= created_at)
);
CREATE INDEX person_notice_unread_idx ON public.person_notice (person_id, id DESC) WHERE read_at IS NULL;
-- A person holds at most one unread notice that the domains stopped accepting writes, however many verifications failed meanwhile.
CREATE UNIQUE INDEX person_notice_one_unread_read_only_idx ON public.person_notice (person_id) WHERE code = 'domain_read_only' AND read_at IS NULL;
-- The application adds a notice with the person, the code and the number, and marks one as read; the moments are the server's. A notice already read is not read again.
GRANT SELECT ON public.person_notice TO app;
GRANT INSERT (person_id, code, sessions_ended) ON public.person_notice TO app;
GRANT UPDATE (read_at) ON public.person_notice TO app;
REVOKE DELETE, TRUNCATE ON public.person_notice FROM PUBLIC, CURRENT_USER;
CREATE FUNCTION public.refuse_person_notice_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    IF TG_OP = 'UPDATE' AND OLD.read_at IS NULL AND NEW.read_at IS NOT NULL
        AND (NEW.id, NEW.person_id, NEW.code, NEW.sessions_ended, NEW.created_at) IS NOT DISTINCT FROM (OLD.id, OLD.person_id, OLD.code, OLD.sessions_ended, OLD.created_at) THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'public.person_notice: % refused', TG_OP USING ERRCODE = 'CE001', SCHEMA = 'public', TABLE = 'person_notice';
END
$refuse$;
CREATE TRIGGER person_notice_only_read BEFORE UPDATE OR DELETE ON public.person_notice FOR EACH ROW EXECUTE FUNCTION public.refuse_person_notice_change();
CREATE TRIGGER person_notice_never_emptied BEFORE TRUNCATE ON public.person_notice FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_person_notice_change();
ALTER TABLE public.person_notice ENABLE ALWAYS TRIGGER person_notice_only_read;
ALTER TABLE public.person_notice ENABLE ALWAYS TRIGGER person_notice_never_emptied;
-- A verdict that the chain failed tells every active person that the writing is suspended (policy 005). The trigger is on the verdict, so the verification the host runs and the one someone asks for
-- tell it alike, in the same transaction as the verdict; a person with the notice still unread is not told twice.
CREATE FUNCTION public.notify_domain_read_only() RETURNS trigger
LANGUAGE plpgsql
AS $notify$
BEGIN
    -- With the window of a restoration open the verdicts of the copy come back, and one of the past tells nobody anything now.
    IF EXISTS (SELECT 1 FROM public.restore_window WHERE state = 'opened') AND NOT EXISTS (SELECT 1 FROM public.restore_window WHERE state = 'closed') THEN RETURN NULL; END IF;
    INSERT INTO public.person_notice (person_id, code) SELECT p.id, 'domain_read_only' FROM public.person p WHERE p.active ON CONFLICT DO NOTHING;
    RETURN NULL;
END
$notify$;
CREATE TRIGGER custody_event_notifies_read_only AFTER INSERT ON public.custody_event FOR EACH ROW
    WHEN (NEW.step = 'verify_chain' AND NEW.reason = 'verification_failed') EXECUTE FUNCTION public.notify_domain_read_only();
