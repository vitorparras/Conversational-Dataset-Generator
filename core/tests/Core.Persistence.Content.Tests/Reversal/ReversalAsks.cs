using System.Globalization;
using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Content.Reversal;
using Core.Persistence.Tests.Content;

namespace Core.Persistence.Tests.Reversal;

/// <summary>Requests asked through the storage, as the application asks them, over an item written with its markers: what the tests of the storage of the reversal share.</summary>
internal static class ReversalAsks
{
    /// <summary>The last event of the chain: kind, item, step, reason, reference and who acted.</summary>
    public const string LastEvent =
        "SELECT item_kind || '|' || item_id || '|' || step || '|' || reason || '|' || coalesce(reference, '') || '|' || actor_person_id FROM public.custody_event "
        + "ORDER BY sequence DESC LIMIT 1";

    public const string CountRequests = "SELECT count(*) FROM content.reversal_request";

    public const string CountAuthorizations = "SELECT count(*) FROM content.reversal_authorization";

    public const string CountEvents = "SELECT count(*) FROM public.custody_event";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static ReversalRequestDraft Draft(long item, string marker) =>
        new() { Item = item.ToString(CultureInfo.InvariantCulture), Marker = marker, Term = "30", Reason = "zqx-porque", Use = "zqx-uso" };

    /// <summary>An item of the domain that holds a marker, written as the application writes it.</summary>
    public static async Task<(long Item, string Marker)> ItemAsync(ContentWorld world, int domainId)
    {
        var marker = PseudonymizedTexts.Draw(1)[0];
        var written = await world.RealWriter.AddPseudonymizedItemAsync(domainId, domainId, PseudonymizedTexts.Holding(marker), Token);
        return (Assert.NotNull(written.ItemId), marker);
    }

    /// <summary>A request of the curator of the world, the person 3, in the domain 1, asked through the storage.</summary>
    public static async Task<long> AskedAsync(ContentWorld world)
    {
        var (item, marker) = await ItemAsync(world, 1);
        var asked = await world.ReversalRequests.AskAsync(1, 3, Draft(item, marker), Token);
        Assert.Equal(ReversalOutcome.Recorded, asked.Outcome);
        return Assert.NotNull(asked.Id);
    }

    public static async Task<long> SecondCuratorAsync(ContentWorld world, string name = "zqx-curator-2") =>
        (long)(await world.ScalarAsync("INSERT INTO public.person (name, function_name) VALUES (@name, 'Curadoria do acervo') RETURNING id", ("name", name)))!;

    public static async Task<long> CountAsync(ContentWorld world, string statement) => (long)(await world.OwnerInDomainAsync(1, statement))!;

    public static async Task<string> LastEventAsync(ContentWorld world) => (string)(await world.ScalarAsync(LastEvent))!;
}
