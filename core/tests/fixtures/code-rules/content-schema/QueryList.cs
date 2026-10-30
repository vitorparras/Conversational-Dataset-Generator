namespace Fixture.ContentSchema;

internal static class ContentQueries
{
    public const string Plain = "SELECT id FROM content.item";

    public static string Interpolated(int id) => $"SELECT id FROM content.item WHERE id = {id}";

    public const string Raw = """
        DELETE FROM content.item
        """;
}
