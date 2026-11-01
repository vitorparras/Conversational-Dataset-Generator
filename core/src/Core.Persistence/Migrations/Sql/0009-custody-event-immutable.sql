-- A custody event is never changed, removed nor emptied away, and the refusal is in the database for every role, the owner included. The privileges of the previous step already
-- leave nobody with UPDATE, DELETE or TRUNCATE; the triggers refuse in the server as well, so that an owner who grants the privilege back, or who runs a script of migration, meets the same refusal.
-- The refusal carries an SQLSTATE of its own (CE001) and a message that names the table, the operation and the sequence of the event, and no value of the event.
CREATE FUNCTION public.refuse_custody_event_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'public.custody_event: % refused for the event of sequence %', TG_OP, OLD.sequence
        USING ERRCODE = 'CE001', SCHEMA = 'public', TABLE = 'custody_event';
END
$refuse$;

-- TRUNCATE fires once for the statement, which has no row: the message names the table and the operation.
CREATE FUNCTION public.refuse_custody_event_emptying() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'public.custody_event: % refused', TG_OP
        USING ERRCODE = 'CE001', SCHEMA = 'public', TABLE = 'custody_event';
END
$refuse$;

CREATE TRIGGER custody_event_never_changed
    BEFORE UPDATE OR DELETE ON public.custody_event
    FOR EACH ROW EXECUTE FUNCTION public.refuse_custody_event_change();

CREATE TRIGGER custody_event_never_emptied
    BEFORE TRUNCATE ON public.custody_event
    FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_custody_event_emptying();

-- ALWAYS, so the refusal also holds in a session that set session_replication_role to replica, which skips an ordinary trigger. The startup check requires this mode.
ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed;
ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_emptied;
