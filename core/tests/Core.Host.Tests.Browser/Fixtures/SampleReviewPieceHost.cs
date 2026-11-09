using System.Globalization;
using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Persistence.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the pieces of the review by sample (issue 610), which have no route of their own yet: the review and the rejection at addresses that exist for the test, behind the real
/// session and screen gates, over a sample of five items drawn for the batch 41. The state is in the address (<c>e=revisados-N</c> says N items reviewed, <c>e=aprovada</c> the sample
/// reviewed and the batch approved, <c>e=reprovada</c> two items reviewed and the batch rejected at the third), and the posts answer as the server will: the mark of an item and the
/// approval go back to the review by a redirect.
/// </summary>
internal sealed class SampleReviewPieceHost : ScreenHostBase
{
    /// <summary>The review of the sample, and the base of the addresses under it that the screen posts and links to.</summary>
    public const string Review = "/captura/revisao";

    /// <summary>The rejection, reached directly; the link of a finding leads to the same page by the address of its item.</summary>
    public const string Reject = "/captura/reprovar";

    /// <summary>How many items the sample has.</summary>
    public const int Items = 5;

    private const string ReviewedState = "revisados-";

    private static readonly SampleReviewScreen.Rejection Finding = new("zqx-curadoria", new DateTimeOffset(2026, 11, 9, 9, 45, 0, TimeSpan.Zero), 3, "contato");

    private SampleReviewPieceHost(ScratchDatabase database)
        : base(database)
    {
    }

    /// <summary>The text of an item of the sample, made up for the run: it must appear beside the item and nowhere else.</summary>
    public static string TextOf(int position) => $"ZQX-texto-do-item-{position}-da-amostra";

    public static Task<SampleReviewPieceHost> StartAsync(CancellationToken cancellationToken) =>
        StartAsync(database => new SampleReviewPieceHost(database), _ => Task.CompletedTask, cancellationToken);

    protected override void AddServices(IServiceCollection services)
    {
    }

    protected override void MapScreens(IEndpointRouteBuilder routes)
    {
        _ = routes.MapGet(Review, ShowReview).WithMetadata(ScreenAccess.Everyone);
        _ = routes.MapGet(Reject, ShowReject).WithMetadata(ScreenAccess.Everyone);
        _ = routes.MapGet(Review + "/itens/{position:int}/achado", ShowFinding).WithMetadata(ScreenAccess.Everyone);
        _ = routes.MapPost(Review + "/itens/{position:int}/revisado", Mark).WithMetadata(ScreenAccess.Everyone);
        _ = routes.MapPost(Review + "/aprovar", Approve).WithMetadata(ScreenAccess.Everyone);
    }

    private static RazorComponentResult<ScreenFrame> ShowReview(HttpContext context)
    {
        var state = context.Request.Query["e"].ToString();
        var approved = state == "aprovada";
        var rejected = state == "reprovada";
        var reviewed = state switch { "aprovada" => Items, "reprovada" => 2, _ => ReviewedIn(state) };
        var items = Enumerable.Range(1, Items).Select(position => new SampleReviewScreen.Item(position, TextOf(position), position != 4, position <= reviewed)).ToList();
        SampleReviewScreen.Approval? approval = approved ? new("zqx-curadoria", new DateTimeOffset(2026, 11, 9, 9, 30, 0, TimeSpan.Zero), "amostra-41") : null;
        return ScreenResult.Of(context, typeof(SampleReviewScreen), SampleReviewScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(SampleReviewScreen.Sample)] = new SampleReviewScreen.Drawn(41, "ZQX-dominio-amostra", Items, items, approval) { Rejected = rejected ? Finding : null },
            [nameof(SampleReviewScreen.Action)] = Review,
        });
    }

    private static RazorComponentResult<ScreenFrame> ShowReject(HttpContext context) =>
        RejectOf(context, 3, context.Request.Query["e"] == "tipo" ? "contato" : null);

    private static RazorComponentResult<ScreenFrame> ShowFinding(int position, HttpContext context) => RejectOf(context, position, null);

    private static RazorComponentResult<ScreenFrame> RejectOf(HttpContext context, int position, string? finding) =>
        ScreenResult.Of(context, typeof(SampleRejectScreen), SampleRejectScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(SampleRejectScreen.Batch)] = 41L,
            [nameof(SampleRejectScreen.Position)] = position,
            [nameof(SampleRejectScreen.Action)] = Review + "/reprovar",
            [nameof(SampleRejectScreen.Back)] = $"{Review}?e={ReviewedState}{(position - 1).ToString(CultureInfo.InvariantCulture)}",
            [nameof(SampleRejectScreen.Finding)] = finding,
        });

    private static IResult Mark(int position, HttpContext context) =>
        PageRedirect.Of(context, $"{Review}?e={ReviewedState}{position.ToString(CultureInfo.InvariantCulture)}");

    private static IResult Approve(HttpContext context) => PageRedirect.Of(context, $"{Review}?e=aprovada");

    private static int ReviewedIn(string state) =>
        state.StartsWith(ReviewedState, StringComparison.Ordinal) && int.TryParse(state[ReviewedState.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? Math.Min(count, Items)
            : 0;
}
