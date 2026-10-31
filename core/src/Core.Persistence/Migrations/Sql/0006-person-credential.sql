-- How a person proves to be who they are: what the derivation of the password left, never the password. One row per person, in a table of its own so that what the audit reads of a person
-- (public.person) never reaches the salt or the result. The algorithm and the work factor travel with the row, so that raising the factor or changing the algorithm is a recomputation at the person's next entry.
CREATE TABLE public.person_credential (
    person_id bigint PRIMARY KEY REFERENCES public.person (id),
    algorithm text NOT NULL CHECK (algorithm <> '' AND algorithm = btrim(algorithm)),
    work_factor integer NOT NULL CHECK (work_factor > 0),
    salt bytea NOT NULL CHECK (octet_length(salt) >= 16),
    derived bytea NOT NULL CHECK (octet_length(derived) > 0),
    derived_at timestamptz NOT NULL DEFAULT now(),
    created_by text NOT NULL DEFAULT session_user
);

-- The application reads and replaces the derivation and nothing else; nobody deletes, the owner included.
GRANT SELECT ON public.person_credential TO PUBLIC;
GRANT INSERT (person_id, algorithm, work_factor, salt, derived) ON public.person_credential TO PUBLIC;
GRANT UPDATE (algorithm, work_factor, salt, derived, derived_at) ON public.person_credential TO PUBLIC;
REVOKE DELETE, TRUNCATE ON public.person_credential FROM PUBLIC, CURRENT_USER;

-- Setting a password is an administrative act and goes to the history of the person, with no value of the password in it.
ALTER TABLE public.person_change DROP CONSTRAINT person_change_kind_check;
ALTER TABLE public.person_change ADD CONSTRAINT person_change_kind_check
    CHECK (kind IN ('created', 'deactivated', 'function_changed', 'password_set'));
