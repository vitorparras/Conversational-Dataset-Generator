namespace Fixture.CatalogQualification;

internal static class QualifiesTheCatalog
{
    public const string Table = "SELECT oid FROM pg_catalog.pg_class";

    public const string Quoted = "SELECT oid FROM pg_catalog.\"pg_class\"";

    public const string Spaced = "SELECT oid FROM PG_CATALOG . pg_class";

    public const string Function = "SELECT pg_catalog.pg_has_role(oid, 'MEMBER'), pg_catalog.count(*) FROM pg_catalog.pg_roles";

    public const string Syntax = "SELECT coalesce(pg_catalog.min(oid), 0) FROM pg_catalog.pg_class c WHERE c.oid IN (1, 2) AND NOT EXISTS (SELECT FROM pg_catalog.pg_policy p WHERE p.polrelid = c.oid)";

    public const string Literal = "SELECT label FROM public.derivation_label WHERE label = 'pg_class(1)' OR label ~ '[\s()]|pg_roles\.'";

    public const string Message = "o papel da aplicação precisa ler pg_settings e pg_extension (tabela(s) e count(x))";

    public const string Cte = "WITH granted AS (SELECT oid FROM pg_catalog.pg_class) SELECT oid FROM granted";
}
