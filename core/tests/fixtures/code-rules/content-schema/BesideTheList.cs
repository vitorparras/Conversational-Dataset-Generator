namespace Fixture.ContentSchema;

internal static class ContentQueries
{
    public const string Listed = "SELECT id FROM content.item";

    public static class Items
    {
        public const string Count = "SELECT count(*) FROM content.item";
    }
}

internal static class Other
{
    public const string Hidden = "SELECT id FROM content.item";
}
