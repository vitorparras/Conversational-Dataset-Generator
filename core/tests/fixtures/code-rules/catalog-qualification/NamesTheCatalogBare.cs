namespace Fixture.CatalogQualification;

internal static class NamesTheCatalogBare
{
    public const string Table = "SELECT oid FROM pg_class";

    public const string View = "SELECT setting FROM pg_settings WHERE name = 'pgaudit.log'";

    public const string Function = "SELECT pg_has_role(oid, 'MEMBER') FROM pg_catalog.pg_roles";

    public const string Setting = "SELECT current_setting('transaction_isolation')";

    public const string Aggregate = "SELECT count(*) FROM pg_catalog.pg_class";

    public const string Quoted = "SELECT oid FROM \"pg_class\"";

    public const string Beside = "SELECT oid FROM public.pg_class";

    public static string Interpolated(int id) => $"SELECT oid FROM pg_class WHERE oid = {id}";

    public const string Raw = """
        SELECT s.rolname
        FROM pg_roles s
        """;
}
