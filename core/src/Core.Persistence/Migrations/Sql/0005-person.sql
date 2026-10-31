-- A person who operates the system. It lives outside the content, with no domain axis: what a person reaches is decided by grants, which come in a later step.
-- A person is deactivated and never deleted, because the history of what was done points at it. The function is one of those the access policy declares; the code holds that list.
CREATE TABLE public.person (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name text NOT NULL UNIQUE CHECK (name <> '' AND name = btrim(name)),
    function_name text NOT NULL CHECK (function_name <> '' AND function_name = btrim(function_name)),
    active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by text NOT NULL DEFAULT session_user
);

-- The application reads and adds people and changes only the function and the state; nobody deletes, the owner included.
GRANT SELECT, INSERT ON public.person TO PUBLIC;
GRANT UPDATE (function_name, active) ON public.person TO PUBLIC;
REVOKE DELETE, TRUNCATE ON public.person FROM PUBLIC, CURRENT_USER;

-- What was done to a person, by whom and when: rows are only added. The actor is null when nobody acted as a person, as when the first one is created.
CREATE TABLE public.person_change (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    person_id bigint NOT NULL REFERENCES public.person (id),
    kind text NOT NULL CHECK (kind IN ('created', 'deactivated', 'function_changed')),
    previous_function text,
    new_function text,
    actor_id bigint REFERENCES public.person (id),
    changed_at timestamptz NOT NULL DEFAULT now(),
    changed_by text NOT NULL DEFAULT session_user
);

GRANT SELECT, INSERT ON public.person_change TO PUBLIC;
REVOKE UPDATE, DELETE, TRUNCATE ON public.person_change FROM PUBLIC, CURRENT_USER;
