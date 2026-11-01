namespace Fixture.CustodyChain;

internal static class RewritesTheChain
{
    public const string Update = "UPDATE public.custody_event SET step = 'correction'";

    public static string Delete(long sequence) => $"DELETE FROM custody_event WHERE sequence = {sequence}";

    public const string Raw = """
        TRUNCATE TABLE ONLY public.custody_event
        """;

    public const string Upsert = "INSERT INTO public.custody_event (step) VALUES (@step) ON CONFLICT (sequence) DO UPDATE SET step = EXCLUDED.step";

    public const string Merge = "MERGE INTO public.custody_event USING other ON true WHEN MATCHED THEN DELETE";

    public const string Quoted = "update \"public\".\"custody_event\" set reason = 'content_read'";
}
