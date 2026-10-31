namespace Fixture.ContentSchema;

internal static class ContentQueries
{
    public const string Listed = "SELECT id FROM content.item";

    private const string Private = "SELECT id FROM content.item";

    internal const string Internal = "SELECT id FROM content.item";

    public static readonly string NotConstant = "SELECT id FROM content.item";
}
