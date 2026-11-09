-- The e-mail address of a person who operates (issue 635, policy 009), in a table of its own and optional: it serves only to carry the link that resets a password. It is no event of the chain and has
-- no history, so the copy of the data and a restoration leave it out, and an address changed or removed leaves nothing behind. The number of this migration is provisional until the pull request opens.
CREATE TABLE public.person_email (
    person_id bigint PRIMARY KEY REFERENCES public.person (id),
    address text NOT NULL CONSTRAINT person_email_address_shape CHECK (char_length(address) BETWEEN 3 AND 254 AND address ~ '^[^@[:space:]]+@[^@[:space:]]+$'),
    changed_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
-- The application keeps, changes and removes the address of a person; the moment is the server's. Nobody empties the table at once.
GRANT SELECT ON public.person_email TO app;
GRANT INSERT (person_id, address) ON public.person_email TO app;
GRANT UPDATE (address, changed_at) ON public.person_email TO app;
GRANT DELETE ON public.person_email TO app;
REVOKE TRUNCATE ON public.person_email FROM PUBLIC, CURRENT_USER;
-- A person deactivated keeps no address: its one purpose ends with the account (policy 009). The database removes it in the same transaction as the deactivation, whoever deactivates.
CREATE FUNCTION public.forget_person_email() RETURNS trigger
LANGUAGE plpgsql
AS $forget$
BEGIN
    DELETE FROM public.person_email WHERE person_id = NEW.id;
    RETURN NULL;
END
$forget$;
CREATE TRIGGER person_email_forgotten_on_deactivation AFTER UPDATE OF active ON public.person FOR EACH ROW WHEN (OLD.active AND NOT NEW.active)
    EXECUTE FUNCTION public.forget_person_email();
ALTER TABLE public.person ENABLE ALWAYS TRIGGER person_email_forgotten_on_deactivation;
