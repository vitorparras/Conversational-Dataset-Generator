using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Core.Host.Endpoints;
using Core.Host.Tests.Submissions;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Tests.Fixtures;
using static Core.Host.Tests.ReversalScreensWorld;

namespace Core.Host.Tests;

/// <summary>
/// The revelation of a marker on its own page (issues 105 and 616) behind the real gates: the GET only confirms, the POST records the revelation and its event before the identity appears,
/// answers it once with no cache, and every answer after says only when it was revealed; lapsed, used or emptied, it is refused with the reason and the denial recorded; the identity is in no
/// table, log line or answer after; and no one else opens the request.
/// </summary>
[Trait("Category", "Database")]
public sealed class ReversalRevealRoutesTests
{
    private const string CountRevelations = "SELECT count(*)::text FROM content.reversal_revelation";

    private const string Steps = "SELECT string_agg(step, ',' ORDER BY sequence) FROM public.custody_event WHERE step IN ('reversal_revealed', 'reversal_entries_read')";

    private const string RevealedDay = "SELECT to_char(revealed_at AT TIME ZONE 'UTC', 'DD/MM/YYYY') FROM content.reversal_revelation";

    private const string RecordedText =
        "SELECT coalesce(string_agg(e::text, '|'), '') || (SELECT coalesce(string_agg(v::text, '|'), '') FROM content.reversal_revelation v) FROM public.custody_event e";

    private static readonly IReadOnlyDictionary<string, string> Nothing = new Dictionary<string, string>(StringComparer.Ordinal);

    [DatabaseFact]
    public async Task RevealedValueIsInNoTableLogOrCache()
    {
        await using var world = await StartAsync();
        var (request, _) = await AuthorizedAsync(world);

        var revealed = await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing);
        var after = new[]
        {
            await world.GetAsync(ScreenPaths.ReversalMineOf(request)), await world.GetAsync(ScreenPaths.ReversalRevealOf(request)), await world.GetAsync(ScreenPaths.ReversalMine),
            await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing),
        };

        Assert.Contains($"<p class=\"reveal\">{Original}</p>", revealed.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(Original, await world.Submissions.Database.DumpAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain(world.Log.Lines, line => line.Contains(Original, StringComparison.Ordinal));
        Assert.All(after, answer => Assert.DoesNotContain(Original, answer.Page, StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task RevealPostRecordsBeforeTheIdentityAppears()
    {
        await using var world = await StartAsync();
        var (request, _) = await AuthorizedAsync(world);
        var (refusedRequest, _) = await AuthorizedAsync(world);
        var authorization = await world.ScalarAsync($"SELECT id::text FROM content.reversal_authorization WHERE request_id = {request}");

        var revealed = await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing);
        var revelation = await world.ScalarAsync("SELECT id::text FROM content.reversal_revelation");
        var recorded = await world.ScalarAsync(LastEvent.Replace("FROM public.custody_event", "FROM public.custody_event WHERE step = 'reversal_revealed'", StringComparison.Ordinal));
        await world.RefuseInTheChainAsync("reversal_revealed");
        var refused = await world.PostAsync(ScreenPaths.ReversalRevealOf(refusedRequest), Nothing);

        Assert.Equal((HttpStatusCode.OK, $"reversal_revelation|{revelation}|reversal_revealed|administrative_act|{authorization}|{world.Curator}|true"), (revealed.Status, recorded));
        Assert.Contains(Original, revealed.Page, StringComparison.Ordinal);
        Assert.Equal("reversal_revealed,reversal_entries_read", await world.ScalarAsync(Steps));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.Status);
        Assert.Contains("Não revelado:</span> a cadeia de custódia não aceitou o registro, e a identidade não foi mostrada.", refused.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(Original, refused.Page, StringComparison.Ordinal);
        Assert.Equal(("1", "reversal_revealed,reversal_entries_read"), (await world.ScalarAsync(CountRevelations), await world.ScalarAsync(Steps)));
    }

    [DatabaseFact]
    public async Task RevealedPageIsNotCachedAndIsNotShownAgain()
    {
        await using var world = await StartAsync();
        var (request, _) = await AuthorizedAsync(world);

        var (status, cache, page) = await RawPostAsync(world, ScreenPaths.ReversalRevealOf(request));
        var day = await world.ScalarAsync(RevealedDay);
        var mine = await world.GetAsync(ScreenPaths.ReversalMineOf(request));
        var confirm = await world.GetAsync(ScreenPaths.ReversalRevealOf(request));
        var list = await world.GetAsync($"{ScreenPaths.ReversalMine}?situacao=revelado");

        Assert.Equal((HttpStatusCode.OK, true), (status, cache.Contains("no-store", StringComparison.Ordinal)));
        Assert.Contains(Original, page, StringComparison.Ordinal);
        Assert.Contains("Esta é a única vez que ela aparece.", page, StringComparison.Ordinal);
        Assert.Contains($"Revelado em {day}, ", mine.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(ScreenPaths.ReversalRevealOf(request), mine.Page, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.ReversalMineOf(request)), (confirm.Status, confirm.Location));
        Assert.Contains($"href=\"{ScreenPaths.ReversalMineOf(request)}\"", list.Page, StringComparison.Ordinal);
        Assert.Contains($"Revelado em {day}</span>", list.Page, StringComparison.Ordinal);
        Assert.All([mine, confirm, list], answer => Assert.DoesNotContain(Original, answer.Page, StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task ExpiredRequestIsRefusedOnReveal()
    {
        await using var world = await StartAsync();
        var (request, _) = await AuthorizedAsync(world);
        var (shown, authorization) = (world.TomorrowShown, await world.ScalarAsync("SELECT id::text FROM content.reversal_authorization"));
        world.Clock.Advance(TimeSpan.FromDays(3));

        var refused = await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing);

        Assert.Equal(HttpStatusCode.OK, refused.Status);
        Assert.Contains($"<h1>Pedido {request}</h1>", refused.Page, StringComparison.Ordinal);
        Assert.Contains($"Não revelado:</span> a autorização venceu em {shown}.", refused.Page, StringComparison.Ordinal);
        Assert.Contains($"href=\"{ScreenPaths.ReversalAsk}\"", refused.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(Original, refused.Page, StringComparison.Ordinal);
        Assert.Equal(
            ($"reversal_authorization|{authorization}|reversal_denied|reversal_expired||{world.Curator}|true", "0"),
            (await world.ScalarAsync(LastEvent), await world.ScalarAsync(CountRevelations)));
    }

    [DatabaseFact]
    public async Task AlreadyRevealedRequestIsRefusedOnReveal()
    {
        await using var world = await StartAsync();
        var (request, _) = await AuthorizedAsync(world);
        var authorization = await world.ScalarAsync("SELECT id::text FROM content.reversal_authorization");
        _ = await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing);

        var again = await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing);

        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.Contains($"Não revelado:</span> este pedido já foi revelado em {await world.ScalarAsync(RevealedDay)}.", again.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(Original, again.Page, StringComparison.Ordinal);
        Assert.Equal(
            ($"reversal_authorization|{authorization}|reversal_denied|reversal_used||{world.Curator}|true", "1"),
            (await world.ScalarAsync(LastEvent), await world.ScalarAsync(CountRevelations)));
    }

    [DatabaseFact]
    public async Task RecordAndLogCarryNoRevealedIdentity()
    {
        await using var world = await StartAsync();
        var (request, _) = await AuthorizedAsync(world);

        var revealed = await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing);
        _ = await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing);

        Assert.Contains(Original, revealed.Page, StringComparison.Ordinal);
        var recorded = await world.ScalarAsync(RecordedText);
        Assert.Contains("reversal_revealed", recorded, StringComparison.Ordinal);
        Assert.Contains("reversal_denied", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain(Original, recorded, StringComparison.Ordinal);
        Assert.NotEmpty(world.Log.Lines);
        Assert.DoesNotContain(world.Log.Lines, line => line.Contains(Original, StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task RevealIsConfirmedOnItsOwnPage()
    {
        await using var world = await StartAsync();
        var (request, _) = await AuthorizedAsync(world);
        var (pending, _) = await world.AskedAsync();
        var marker = await world.ScalarAsync($"SELECT marker FROM content.reversal_request WHERE id = {request}");
        var events = await world.ScalarAsync(CountEvents);

        var mine = await world.GetAsync(ScreenPaths.ReversalMineOf(request));
        var confirm = await world.GetAsync(ScreenPaths.ReversalRevealOf(request));
        var notYet = await world.GetAsync(ScreenPaths.ReversalRevealOf(pending));
        var pendingPage = await world.GetAsync(ScreenPaths.ReversalMineOf(pending));

        Assert.Contains($"href=\"{ScreenPaths.ReversalRevealOf(request)}\">Revelar a identidade</a>", mine.Page, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, confirm.Status);
        Assert.Contains($"<h1>Revelar <code>{marker}</code>?</h1>", confirm.Page, StringComparison.Ordinal);
        Assert.Contains("<li>A revelação fica registrada antes de a identidade aparecer.</li>", confirm.Page, StringComparison.Ordinal);
        Assert.Contains("<li>Ela aparece uma só vez. Voltar ou recarregar não a mostra de novo.</li>", confirm.Page, StringComparison.Ordinal);
        Assert.Contains($"method=\"post\" action=\"{ScreenPaths.ReversalRevealOf(request)}\"", confirm.Page, StringComparison.Ordinal);
        Assert.Matches("<button id=\"revelar\" class=\"button button--danger\"[^>]*>Revelar agora</button>", confirm.Page);
        Assert.Contains($"href=\"{ScreenPaths.ReversalMineOf(request)}\">Voltar sem revelar</a>", confirm.Page, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.ReversalMineOf(pending)), (notYet.Status, notYet.Location));
        Assert.DoesNotContain(ScreenPaths.ReversalRevealOf(pending), pendingPage.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(Original, confirm.Page, StringComparison.Ordinal);
        Assert.Equal((events, "0"), (await world.ScalarAsync(CountEvents), await world.ScalarAsync(CountRevelations)));
    }

    [DatabaseFact]
    public async Task RevealOfAnEmptiedItemIsDeniedWithTheReason()
    {
        await using var world = await StartAsync();
        var (request, item) = await AuthorizedAsync(world);
        var authorization = await world.ScalarAsync("SELECT id::text FROM content.reversal_authorization");
        var emptied = await ItemEmptyingDesk.On(world.Submissions.DataSource)
            .EmptyAsync(world.Curator, new(SubmissionWorld.Domain, item, EmptyingMotive.ContractRequirement, "PROT-2026/0042"), TestContext.Current.CancellationToken);
        var probe = await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing);
        var (last, count) = (await world.ScalarAsync(LastEvent), await world.ScalarAsync(CountRevelations));
        var h1 = Regex.Match(probe.Page, "<h1>[^<]{0,80}", RegexOptions.None, TimeSpan.FromSeconds(1)).Value;
        var notice = Regex.Match(probe.Page, "notice__kind[^\n]{0,200}", RegexOptions.None, TimeSpan.FromSeconds(1)).Value;
        var said = string.Create(CultureInfo.InvariantCulture, $"error: diag emptied={emptied.Outcome} refusal={emptied.Refusal?.Outcome}:{emptied.Refusal?.Refusal} status={probe.Status}");
        var ask = probe.Page.IndexOf($"href=\"{ScreenPaths.ReversalAsk}\"", StringComparison.Ordinal);
        var near = ask < 0 ? string.Empty : probe.Page.Substring(Math.Max(0, ask - 160), 200).ReplaceLineEndings(" ");
        var original = probe.Page.Contains(Original, StringComparison.Ordinal);
        if (ask >= 0 || original)
        {
            Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"{said} last={last} count={count} h1={h1} notice={notice} original={original} ask={near}"));
        }

        Assert.Equal(EmptyingOutcome.Emptied, emptied.Outcome);

        var refused = await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing);

        Assert.Equal(HttpStatusCode.OK, refused.Status);
        Assert.Contains($"Não revelado:</span> o item {item} foi esvaziado, e o mapa dele não existe mais.", refused.Page, StringComparison.Ordinal);
        Assert.DoesNotContain($"href=\"{ScreenPaths.ReversalAsk}\"", refused.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(Original, refused.Page, StringComparison.Ordinal);
        Assert.Equal(
            ($"reversal_authorization|{authorization}|reversal_denied|reversal_emptied||{world.Curator}|true", "0"),
            (await world.ScalarAsync(LastEvent), await world.ScalarAsync(CountRevelations)));
    }

    [DatabaseFact]
    public async Task OthersCannotOpenMyRequest()
    {
        await using var world = await StartAsync();
        var (request, _) = await AuthorizedAsync(world);
        var events = await world.ScalarAsync(CountEvents);

        var answers = new[]
        {
            await world.GetAsync(ScreenPaths.ReversalRevealOf(request), world.Second), await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing, world.Second),
            await world.GetAsync(ScreenPaths.ReversalRevealOf(request), world.Other), await world.PostAsync(ScreenPaths.ReversalRevealOf(request), Nothing, world.Other),
            await world.GetAsync(ScreenPaths.ReversalRevealOf(request + 100)), await world.PostAsync(ScreenPaths.ReversalRevealOf(request + 100), Nothing),
        };

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.NotFound, answer.Status));
        Assert.All(answers, answer => Assert.DoesNotContain(Original, answer.Page, StringComparison.Ordinal));
        Assert.Equal((events, "0"), (await world.ScalarAsync(CountEvents), await world.ScalarAsync(CountRevelations)));
        Assert.Contains("Autorizado até", (await world.GetAsync(ScreenPaths.ReversalMineOf(request))).Page, StringComparison.Ordinal);
    }

    /// <summary>A request of the first of the curation, authorized by the second until tomorrow.</summary>
    private static async Task<(long Request, long Item)> AuthorizedAsync(ReversalScreensWorld world)
    {
        var asked = await world.AskedAsync();
        var decided = await world.PostAsync(
            ScreenPaths.ReversalDecideOf(asked.Id), new Dictionary<string, string>(StringComparer.Ordinal) { ["decisao"] = "autorizar", ["validade"] = world.Tomorrow }, world.Second);
        Assert.Equal(HttpStatusCode.SeeOther, decided.Status);
        return asked;
    }

    /// <summary>A POST of the first of the curation with the headers of its answer, which the world's replies leave out.</summary>
    private static async Task<(HttpStatusCode Status, string Cache, string Page)> RawPostAsync(ReversalScreensWorld world, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(route, UriKind.Relative)) { Content = new FormUrlEncodedContent(Nothing) };
        request.Headers.Accept.ParseAdd(SampleReviewWorld.Browser);
        using var response = await world.Host.SendAsync(request, world.Host.Sessions.Open(world.Curator), encrypted: true, TestContext.Current.CancellationToken);
        return (response.StatusCode, response.Headers.CacheControl?.ToString() ?? "", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }
}
