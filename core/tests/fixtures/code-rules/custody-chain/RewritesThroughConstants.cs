namespace Fixture.CustodyChain;

internal static class RewritesThroughConstants
{
    private const string Verb = "UPDATE ";
    private const string Table = "public.custody_event";

    public const string Joined = "UPDATE " + "public.custody_event" + " SET step = 'correction'";

    public const string ByName = Verb + Table + " SET reason = 'content_read'";

    public const string Interpolated = $"{Verb}{Table} SET reason = 'content_read'";

    public static string Parenthesized(long sequence) => ("DELETE FROM " + "custody_event") + $" WHERE sequence = {sequence}";

    public const string Lines = "TRUNCATE " +
        "public.custody_event";
}
