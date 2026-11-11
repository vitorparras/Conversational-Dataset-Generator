using System.Globalization;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The writing that the database stopped behind another (issue 74): it is tried again, up to three times in all, and the last attempt tells the failure as it came.
/// A trigger of the test refuses the first attempts with a serialization failure and counts them in a sequence, which a rollback does not undo.
/// </summary>
public sealed class ContentWriterRetryTests
{
    private const string Attempts = "SELECT last_value FROM public.zqx_attempts";

    [DatabaseTheory]
    [InlineData("reversal_map", 0, 1, true)]
    [InlineData("reversal_map", 1, 2, true)]
    [InlineData("reversal_map", 2, 3, true)]
    [InlineData("reversal_map", 3, 3, false)]
    [InlineData("reversal_map", 100, 3, false)]
    [InlineData("item_text", 0, 1, true)]
    [InlineData("item_text", 1, 2, true)]
    [InlineData("item_text", 2, 3, true)]
    [InlineData("item_text", 3, 3, false)]
    public async Task AWriteStoppedBehindAnotherIsTriedAgainUpToThreeTimesAndTheLastFailureIsTold(string table, int refusedFirst, long attempts, bool written)
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, ItemTexts.Of("texto bruto zqx"), TestContext.Current.CancellationToken);
        var before = await StoredAsync(world, table);
        await world.Database.RunAsOwnerAsync(RefusingTrigger(table, refusedFirst));

        var outcome = await Record.ExceptionAsync(() => WriteAsync(world, table, item));
        var tried = Convert.ToInt64(await world.ScalarAsync(Attempts), CultureInfo.InvariantCulture);

        Assert.Equal(
            (written, written ? null : "40001", attempts, before + (written ? 1 : 0)),
            (outcome is null, (outcome as PostgresException)?.SqlState, tried, await StoredAsync(world, table)));
    }

    private static Task<long> WriteAsync(ContentWorld world, string table, long item) => table == "item_text"
        ? world.Writer.AddItemTextAsync(1, ItemTexts.Of("outro texto zqx"), TestContext.Current.CancellationToken)
        : world.Writer.AddReversalMapAsync(1, item, "mapa zqx", TestContext.Current.CancellationToken);

    private static async Task<long> StoredAsync(ContentWorld world, string table) =>
        (long)(await world.OwnerInDomainAsync(1, table == "item_text" ? "SELECT count(*) FROM content.item_text" : "SELECT count(*) FROM content.reversal_map"))!;

    private static string[] RefusingTrigger(string table, int refusedFirst) =>
    [
        "CREATE SEQUENCE public.zqx_attempts",
        "CREATE FUNCTION public.zqx_refuse() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $f$ "
            + $"BEGIN IF pg_catalog.nextval('public.zqx_attempts') <= {refusedFirst} THEN RAISE EXCEPTION 'zqx' USING ERRCODE = '40001'; END IF; RETURN NEW; END $f$",
        $"CREATE TRIGGER aaa_zqx_refuse BEFORE INSERT ON content.{table} FOR EACH ROW EXECUTE FUNCTION public.zqx_refuse()",
        "SELECT pg_catalog.setval('public.zqx_attempts', 1, false)",
    ];
}
