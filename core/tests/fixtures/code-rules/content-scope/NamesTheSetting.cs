namespace Fixture.ContentScope;

internal static class NamesTheSetting
{
    public const string Plain = "SELECT set_config('app.content_scope', 'system', true)";

    public static string Interpolated(string scope) => $"SELECT set_config('app.content_scope', '{scope}', true)";
}
