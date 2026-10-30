namespace Fixture.ContentSchema;

internal static class MovesTheSearchPath
{
    public const string Set = "SET search_path = content";

    public const string Function = "SELECT set_config('search_path', 'content', false)";

    public const string Connection = "Host=zqx;Search Path=content";

    public const string Role = "ALTER ROLE app SET SEARCH_PATH TO content";
}
