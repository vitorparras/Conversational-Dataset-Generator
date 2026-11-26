using Core.Persistence.Content.Reversal;
using Core.Persistence.Tests.Content;
using Npgsql;

namespace Core.Persistence.Tests.Reversal;

/// <summary>Requests asked and authorized through the storage, as the application does, and the revelation of them: what the tests of the revelation share.</summary>
internal static class ReversalReveals
{
    public const string CountRevelations = "SELECT count(*) FROM content.reversal_revelation";

    public const string CountOfStep = "SELECT count(*) FROM public.custody_event WHERE step = @step";

    /// <summary>How many events of the step whose first ones the chain refuses reached it, the refused included.</summary>
    public const string AttemptsInTheChain = "SELECT last_value FROM public.zqx_chain_attempts";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The act of the curator of the world, the person 3, who asked, on the request of the domain 1.</summary>
    public static ReversalAct ByWhoAsked(long request) => new(1, request, 3);

    /// <summary>A request of the curator of the world, the person 3, in the domain 1, authorized for 30 days by another person of the curation, both through the storage.</summary>
    public static async Task<Authorized> AuthorizedAsync(ContentWorld world)
    {
        var (item, marker) = await ReversalAsks.ItemAsync(world, 1);
        var asked = await world.ReversalRequests.AskAsync(1, 3, ReversalAsks.Draft(item, marker), Token);
        var request = Assert.NotNull(asked.Id);
        var second = await ReversalAsks.SecondCuratorAsync(world, $"zqx-curator-{marker}");
        var authorized = await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, second), DateTimeOffset.UtcNow.AddDays(30), Token);
        Assert.Equal(ReversalOutcome.Recorded, authorized.Outcome);
        return new Authorized(request, Assert.NotNull(authorized.Id), marker, item);
    }

    /// <summary>How many events of the step the chain holds.</summary>
    public static async Task<long> CountOfStepAsync(ContentWorld world, string step) => (long)(await world.ScalarAsync(CountOfStep, ("step", step)))!;

    /// <summary>Makes the chain refuse, as broken, every event of the step given from now on, and only those.</summary>
    public static Task RefuseInTheChainAsync(ContentWorld world, string step) =>
        world.Database.ExecuteAsSuperuserAsync(
            "CREATE FUNCTION public.zqx_chain_refuses() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $f$ "
            + "BEGIN RAISE EXCEPTION 'broken' USING ERRCODE = 'CE002'; END $f$; "
            + $"CREATE TRIGGER zqx_chain_refuses BEFORE INSERT ON public.custody_event FOR EACH ROW WHEN (NEW.step = '{step}') EXECUTE FUNCTION public.zqx_chain_refuses()");

    /// <summary>
    /// Makes the chain refuse the first events of the step given with the state given, as a conflict with another writer (40001) or as broken (CE002), and take the ones after them;
    /// the sequence counts the events of the step that reached the chain, because a sequence is not undone with the transaction refused.
    /// </summary>
    public static Task RefuseTheFirstInTheChainAsync(ContentWorld world, string step, int refusals, string sqlState) =>
        world.Database.ExecuteAsSuperuserAsync(
            "CREATE SEQUENCE public.zqx_chain_attempts; GRANT SELECT ON SEQUENCE public.zqx_chain_attempts TO PUBLIC; "
            + "CREATE FUNCTION public.zqx_chain_refuses_first() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $f$ "
            + $"BEGIN IF nextval('public.zqx_chain_attempts') <= {refusals} THEN RAISE EXCEPTION 'refused' USING ERRCODE = '{sqlState}'; END IF; RETURN NEW; END $f$; "
            + $"CREATE TRIGGER zqx_chain_refuses_first BEFORE INSERT ON public.custody_event FOR EACH ROW WHEN (NEW.step = '{step}') EXECUTE FUNCTION public.zqx_chain_refuses_first()");

    /// <summary>
    /// Holds the map of every item locked, so that no reading of it goes through until the hold is let go: a revelation that opened the map before its event was committed would
    /// wait here with its event not yet seen by another connection.
    /// </summary>
    public static async Task<NpgsqlTransaction> HoldTheMapAsync(NpgsqlConnection holder)
    {
        await holder.OpenAsync(Token);
        var hold = await holder.BeginTransactionAsync(Token);
        await using var locking = new NpgsqlCommand("LOCK TABLE content.reversal_map IN ACCESS EXCLUSIVE MODE", holder, hold);
        _ = await locking.ExecuteNonQueryAsync(Token);
        return hold;
    }

    /// <summary>The last event of the chain, read by another connection, once it is the revelation; empty when the revelation ends, or ten seconds go, before it is seen.</summary>
    public static async Task<string> RevelationSeenAsync(ContentWorld world, IAsyncResult revealing)
    {
        for (var tries = 0; tries < 100 && !revealing.IsCompleted; tries++)
        {
            var last = await ReversalAsks.LastEventAsync(world);
            if (last.Contains("|reversal_revealed|", StringComparison.Ordinal))
            {
                return last;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Token);
        }

        return string.Empty;
    }

    /// <summary>A request authorized: the request, its authorization, its marker and its item.</summary>
    internal sealed record Authorized(long Request, long Authorization, string Marker, long Item);
}
