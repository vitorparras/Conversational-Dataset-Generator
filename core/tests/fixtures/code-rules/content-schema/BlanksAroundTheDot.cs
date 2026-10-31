namespace Fixture.ContentSchema;

internal static class BlanksAroundTheDot
{
    public const string Both = "SELECT id FROM content . item";

    public const string Before = "SELECT id FROM content .item";

    public const string After = "SELECT id FROM content. item";

    public const string Quoted = "SELECT id FROM \"content\" . \"item\"";

    public const string Raw = """
        DELETE FROM content
            .
            item
        """;

    public const string Upper = "SELECT id FROM CONTENT . Item";
}
