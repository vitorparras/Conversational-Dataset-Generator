-- One row per derivation label already used to encrypt; the table is born empty and the steps that encrypt fill it.
-- A label is never dropped, because the data encrypted under it is unreadable without it.
-- No white space around a label, the tab and the line break included, as DerivationLabels refuses it: a label the table took and the code refused would block the start for good.
CREATE TABLE public.derivation_label (
    label text PRIMARY KEY CHECK (label <> '' AND label !~ '^\s|\s$'),
    first_used_at timestamptz NOT NULL DEFAULT now(),
    first_used_by text NOT NULL DEFAULT session_user
);

-- Read-only for everyone, as the registry: the startup check reads it with the application's role, and the steps that encrypt grant the writing.
GRANT SELECT ON public.derivation_label TO PUBLIC;
