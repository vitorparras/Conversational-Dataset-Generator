-- A label is never changed nor dropped, because the data encrypted under it is unreadable without it. The refusal is in the database, for every role,
-- the owner included: the table is read by the startup check and written only by INSERT.
CREATE FUNCTION public.refuse_derivation_label_change() RETURNS trigger
LANGUAGE plpgsql
AS $refuse$
BEGIN
    RAISE EXCEPTION 'a derivation label is never changed nor dropped (% on %)', TG_OP, TG_TABLE_NAME
        USING ERRCODE = 'restrict_violation';
END
$refuse$;

CREATE TRIGGER derivation_label_never_changed
    BEFORE UPDATE OR DELETE ON public.derivation_label
    FOR EACH ROW EXECUTE FUNCTION public.refuse_derivation_label_change();

CREATE TRIGGER derivation_label_never_emptied
    BEFORE TRUNCATE ON public.derivation_label
    FOR EACH STATEMENT EXECUTE FUNCTION public.refuse_derivation_label_change();

-- ALWAYS, so the refusal also holds in a session that set session_replication_role to replica, which skips an ordinary trigger.
ALTER TABLE public.derivation_label ENABLE ALWAYS TRIGGER derivation_label_never_changed;
ALTER TABLE public.derivation_label ENABLE ALWAYS TRIGGER derivation_label_never_emptied;
