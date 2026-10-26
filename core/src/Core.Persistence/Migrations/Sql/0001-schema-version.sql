-- One row per applied step, written by the transaction that applied it; the migrating role owns the table.
CREATE TABLE public.schema_version (
    version integer PRIMARY KEY CHECK (version > 0),
    name text NOT NULL,
    sha256 text NOT NULL,
    applied_at timestamptz NOT NULL DEFAULT now(),
    applied_by text NOT NULL DEFAULT session_user
);

-- Read-only for everyone: the application's role reads the version, and its name is not fixed here.
GRANT SELECT ON public.schema_version TO PUBLIC;
