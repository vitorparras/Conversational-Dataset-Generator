-- A domain is the axis of every content table: the rows of a content table belong to one, and the database decides what a session sees from it.
CREATE TABLE public.domain (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name text NOT NULL UNIQUE CHECK (name <> '' AND name = btrim(name)),
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by text NOT NULL DEFAULT session_user
);

-- Read-only for everyone, as the registry; creating a domain is a step of the owner.
GRANT SELECT ON public.domain TO PUBLIC;

-- The domain the session declared with SET LOCAL app.domain; null when nobody declared one, so a policy that compares a row with it matches no row.
CREATE FUNCTION public.declared_domain() RETURNS integer
LANGUAGE sql STABLE PARALLEL SAFE
RETURN nullif(current_setting('app.domain', true), '')::integer;

-- The schema that marks a table as content: the startup check looks for a domain column, a policy and FORCE in every table found here.
CREATE SCHEMA content;
GRANT USAGE ON SCHEMA content TO PUBLIC;

-- The mold of a content table: its id, the domain it belongs to (not null, tied to public.domain) and the row policy with FORCE, so that
-- the table never exists without the policy. The columns are the definitions after the two that every content table carries.
CREATE FUNCTION public.create_content_table(table_name text, columns text) RETURNS void
LANGUAGE plpgsql
AS $mold$
BEGIN
    EXECUTE format(
        'CREATE TABLE content.%I (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, domain_id integer NOT NULL REFERENCES public.domain (id), %s)',
        table_name, columns);
    EXECUTE format('ALTER TABLE content.%I ENABLE ROW LEVEL SECURITY', table_name);
    EXECUTE format('ALTER TABLE content.%I FORCE ROW LEVEL SECURITY', table_name);
    EXECUTE format(
        'CREATE POLICY domain_isolation ON content.%I USING (domain_id = public.declared_domain()) WITH CHECK (domain_id = public.declared_domain())',
        table_name);
END
$mold$;

REVOKE ALL ON FUNCTION public.create_content_table(text, text) FROM PUBLIC;
