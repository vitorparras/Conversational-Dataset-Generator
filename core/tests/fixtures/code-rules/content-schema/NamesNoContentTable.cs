namespace Fixture.ContentSchema;

internal static class NamesNoContentTable
{
    public const string Schema = "content";

    public const string Registry = "SELECT version FROM public.schema_version";

    public const string Mold = "SELECT public.create_content_table('item', 'text_hash text NOT NULL')";

    public const string Words = "the content. Then another sentence";
}
