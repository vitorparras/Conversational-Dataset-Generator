using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Host.Tests.Browser.Trial;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the pieces of the detail of a batch (issue 609), which have no route of their own yet: the screen at an address that exists for the test, behind the real session and screen
/// gates, over counts made up for the run. The state is in the address (<c>e</c>): an accepted batch, a rejected one with its motive, one still open with no item, one that lost something
/// when the form was accommodated and one recognized as the resending of another, whose detail is the state <c>anterior</c>.
/// </summary>
internal sealed class BatchDetailPieceHost : ScreenHostBase
{
    /// <summary>The address of the detail, with the state in the query.</summary>
    public const string Detail = "/captura/lote";

    /// <summary>The address the screen gives as the list of batches; it exists only to be linked to.</summary>
    public const string List = "/captura/lotes";

    /// <summary>The domain of the batches, made up for the run.</summary>
    public const string Domain = "ZQX-dominio-do-lote";

    /// <summary>The person who submitted, made up for the run.</summary>
    public const string Submitter = "ZQX-quem-enviou";

    private static readonly DateTimeOffset SubmittedAt = new(2026, 11, 8, 9, 30, 0, TimeSpan.Zero);

    private BatchDetailPieceHost(ScratchDatabase database)
        : base(database)
    {
    }

    public static Task<BatchDetailPieceHost> StartAsync(CancellationToken cancellationToken) =>
        StartAsync(database => new BatchDetailPieceHost(database), _ => Task.CompletedTask, cancellationToken);

    protected override void AddServices(IServiceCollection services)
    {
    }

    protected override void MapScreens(IEndpointRouteBuilder routes)
    {
        _ = routes.MapGet(Detail, Show).WithMetadata(ScreenAccess.Everyone);
        _ = routes.MapGet(List, (HttpContext context) => ScreenResult.Of(context, typeof(BareScreen), "Lotes")).WithMetadata(ScreenAccess.Everyone);
    }

    private static RazorComponentResult<ScreenFrame> Show(HttpContext context)
    {
        var batch = context.Request.Query["e"].ToString() switch
        {
            "rejeitado" => Counted(39, BatchState.Rejected, "order_incoherent"),
            "em-curso" => new BatchDetailScreen.Detail(40, Domain, BatchState.Open, null, Submitter, SubmittedAt, true),
            "perda" => Counted(38, BatchState.Accepted, null) with { Accommodation = new BatchDetailScreen.Accommodated("conversation", "comment", 12, ["author", "instant", "replies_to"]) },
            "reenvio" => Counted(42, BatchState.Accepted, null) with { Recognized = new BatchDetailScreen.Recognition(33, $"{Detail}?e=anterior") },
            "anterior" => Counted(33, BatchState.Accepted, null),
            _ => Counted(41, BatchState.Accepted, null),
        };
        return ScreenResult.Of(context, typeof(BatchDetailScreen), "Lote", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(BatchDetailScreen.Batch)] = batch,
            [nameof(BatchDetailScreen.ListAddress)] = List,
        });
    }

    private static BatchDetailScreen.Detail Counted(long id, BatchState state, string? motive) =>
        new(id, Domain, state, motive, Submitter, SubmittedAt, true)
        {
            Forms = [new BatchDetailScreen.FormCount("conversation", 12), new BatchDetailScreen.FormCount("comment", 3)],
            Destinations = [new BatchDetailScreen.DestinationCount("remove", 4), new BatchDetailScreen.DestinationCount("pseudonymize", 19), new BatchDetailScreen.DestinationCount("preserve", 2)],
            Replicas = new BatchDetailScreen.ReplicaCount(5, 3),
        };
}
