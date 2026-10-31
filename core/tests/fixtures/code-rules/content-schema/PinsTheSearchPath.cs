namespace Fixture.ContentSchema;

internal static class PinsTheSearchPath
{
    public const string Catalog = "SET LOCAL search_path = pg_catalog, pg_temp";

    public const string WithContent = "SET LOCAL search_path = pg_catalog, content";

    public const string ForTheSession = "SET search_path = pg_catalog, pg_temp";

    public const string WithPublic = "SET LOCAL search_path = pg_temp, pg_catalog, public";

    public const string Chained = "SET LOCAL search_path = pg_catalog, pg_temp; SET search_path = content";
}
