-- One row per failed attempt at a step, written by the runner after the step's transaction was undone; rows are only added.
-- It holds the step and the SQLSTATE and nothing the server said or the step contained, so no message and no data reach it.
CREATE TABLE public.schema_failure (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    version integer NOT NULL CHECK (version > 0),
    name text NOT NULL,
    sha256 text NOT NULL,
    sqlstate text CHECK (sqlstate ~ '^[0-9A-Z]{5}$'),
    failed_at timestamptz NOT NULL DEFAULT now(),
    failed_by text NOT NULL DEFAULT session_user
);

-- Read-only for everyone, as the registry; the migrating role owns the table and is the one that writes it.
GRANT SELECT ON public.schema_failure TO PUBLIC;
