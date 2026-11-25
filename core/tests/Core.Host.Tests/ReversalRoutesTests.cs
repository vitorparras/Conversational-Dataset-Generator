using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Core.Host.Endpoints;
using Core.Host.Tests.Submissions;
using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Content.Reversal;
using Core.Persistence.Tests.Fixtures;
using static Core.Host.Tests.ReversalScreensWorld;

namespace Core.Host.Tests;

/// <summary>
/// The screens of the request to reverse a marker and of its authorization (issues 614 and 615) behind the real gates, over a database of their own: the request is one marker in one item, and
/// what reaches more, or a marker the item does not hold, is refused on the screen and recorded; the request tells who is missing and shows no identity; the others of the curation see it,
/// with why and what for, and decide on a page of its own, which records the reading of the text; whoever asked never decides; whoever is not of the curation of the domain gets the answer
/// of a route that does not exist.
/// </summary>
[Trait("Category", "Database")]
public sealed class ReversalRoutesTests
{
    private const string Missing = "/zqx-nao-existe-9741";
    private const string NotInItem = "PESSOA_00000001";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task MarkerNotInTheItemIsRefusedByScopeWithTheReason()
    {
        await using var world = await StartAsync();
        var (item, _) = await world.ItemAsync();

        var page = await world.PostAsync(ScreenPaths.ReversalAsk, Draft(item, NotInItem));

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Matches(Error("marcador", "Este marcador não está no item. A recusa foi registrada."), page.Page);
        Assert.Contains($"value=\"{NotInItem}\"", page.Page, StringComparison.Ordinal);
        Assert.Equal(
            ($"item_text|{item}|reversal_request_refused|reversal_scope|{NotInItem}|{world.Curator}|true", "0"),
            (await world.ScalarAsync(LastEvent), await world.ScalarAsync(CountRequests)));
    }

    [DatabaseFact]
    public async Task AnnotatorRequestIsRefusedAndRecordedWithWhoWhenAndScope()
    {
        await using var world = await StartAsync();
        var (item, marker) = await world.ItemAsync();
        var draft = new ReversalRequestDraft { Item = item.ToString(CultureInfo.InvariantCulture), Marker = marker, Term = "30", Reason = "zqx-motivo", Use = "zqx-uso" };

        using var access = await world.Desk.OpenAsync(Token) ?? throw new InvalidOperationException("The key was not read.");
        var asked = await access.Requests.AskAsync(SubmissionWorld.Domain, Annotator, draft, Token);

        Assert.Equal(ReversalOutcome.NotCuration, asked.Outcome);
        Assert.Equal(
            ($"item_text|{item}|reversal_request_refused|reversal_function|{marker}|{Annotator}|true", "0"),
            (await world.ScalarAsync(LastEvent), await world.ScalarAsync(CountRequests)));
    }

    [DatabaseFact]
    public async Task AuthorizingOwnRequestIsRefusedByTheApplicationAndRecorded()
    {
        await using var world = await StartAsync();
        var (request, _) = await world.AskedAsync();

        var page = await world.PostAsync(ScreenPaths.ReversalDecideOf(request), Decision("autorizar", world.Tomorrow));

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("Você não autoriza o próprio pedido. A tentativa foi registrada.", page.Page, StringComparison.Ordinal);
        Assert.Equal(
            ($"reversal_request|{request}|reversal_authorization_refused|reversal_self||{world.Curator}|true", "0"),
            (await world.ScalarAsync(LastEvent), await world.ScalarAsync(CountAuthorizations)));
    }

    [DatabaseFact]
    public async Task AuthorizationByWhoIsNotCurationOfTheDomainIsRefused()
    {
        await using var world = await StartAsync();
        var (request, _) = await world.AskedAsync();
        var (absentOther, absentAnnotator) = (await world.GetAsync(Missing, world.Other), await world.GetAsync(Missing, Annotator));

        var theirs = await world.PostAsync(ScreenPaths.ReversalDecideOf(request), Decision("autorizar", world.Tomorrow), world.Other);
        var annotator = await world.PostAsync(ScreenPaths.ReversalDecideOf(request), Decision("autorizar", world.Tomorrow), Annotator);
        using var access = await world.Desk.OpenAsync(Token) ?? throw new InvalidOperationException("The key was not read.");
        var direct = await access.Requests.AuthorizeAsync(new ReversalAct(SubmissionWorld.Domain, request, Annotator), world.Clock.GetUtcNow().AddDays(1), Token);

        Assert.Equal((HttpStatusCode.NotFound, WithoutAddress(absentOther.Page)), (theirs.Status, WithoutAddress(theirs.Page)));
        Assert.Equal((HttpStatusCode.NotFound, WithoutAddress(absentAnnotator.Page)), (annotator.Status, WithoutAddress(annotator.Page)));
        Assert.Equal(ReversalOutcome.NotCuration, direct.Outcome);
        Assert.Equal(
            ($"reversal_request|{request}|reversal_authorization_refused|reversal_function||{Annotator}|true", "0"),
            (await world.ScalarAsync(LastEvent), await world.ScalarAsync(CountAuthorizations)));
    }

    [DatabaseFact]
    public async Task IncompleteRequestFormReturnsWithTheErrorsAndAsksNothing()
    {
        await using var world = await StartAsync();
        var (item, marker) = await world.ItemAsync();
        var events = await world.ScalarAsync(CountEvents);
        var partial = Draft(item, marker);
        (partial["prazo"], partial["motivo"]) = (string.Empty, " ");

        var some = await world.PostAsync(ScreenPaths.ReversalAsk, partial);
        var none = await world.PostAsync(ScreenPaths.ReversalAsk, new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (some.Status, none.Status));
        Assert.Matches(Error("prazo", "Diga quantos dias."), some.Page);
        Assert.Matches(Error("motivo", "Diga por que precisa da identidade."), some.Page);
        Assert.Equal(2, Regex.Count(some.Page, "aria-invalid=\"true\"", RegexOptions.None, TimeSpan.FromSeconds(1)));
        Assert.Contains($"value=\"{marker}\"", some.Page, StringComparison.Ordinal);
        Assert.Contains(">zqx-uso-sintetico</textarea>", some.Page, StringComparison.Ordinal);
        Assert.Matches(Error("item", "Diga o número do item."), none.Page);
        Assert.Matches(Error("marcador", "Diga o marcador, como PESSOA_00000000."), none.Page);
        Assert.Matches(Error("uso", "Diga o que fará com ela."), none.Page);
        Assert.Equal(5, Regex.Count(none.Page, "aria-invalid=\"true\"", RegexOptions.None, TimeSpan.FromSeconds(1)));
        Assert.Equal(("0", events), (await world.ScalarAsync(CountRequests), await world.ScalarAsync(CountEvents)));
    }

    [DatabaseFact]
    public async Task DomainWideRequestIsRefusedOnTheScreenAndRecorded()
    {
        await using var world = await StartAsync();
        var (item, marker) = await world.ItemAsync();
        var many = Draft(item, marker);
        many["item"] = $"{item}, 204";
        var markerless = Draft(item, marker);
        markerless["marcador"] = string.Empty;

        var manyPage = await world.PostAsync(ScreenPaths.ReversalAsk, many);
        var manyEvent = await world.ScalarAsync(LastEvent);
        var domainPage = await world.PostAsync(ScreenPaths.ReversalAsk, markerless);

        Assert.Matches(Error("item", "Um item só. A recusa foi registrada."), manyPage.Page);
        Assert.Matches(Error("marcador", "Sem marcador, o pedido alcança o domínio inteiro. A recusa foi registrada."), domainPage.Page);
        Assert.Equal(
            ($"domain|1|reversal_request_refused|reversal_scope|varios-itens|{world.Curator}|true", $"domain|1|reversal_request_refused|reversal_scope|dominio|{world.Curator}|true", "0"),
            (manyEvent, await world.ScalarAsync(LastEvent), await world.ScalarAsync(CountRequests)));
    }

    [DatabaseFact]
    public async Task AnnotatorOpeningTheRequestPageGetsNotFound()
    {
        await using var world = await StartAsync();
        var (request, _) = await world.AskedAsync();
        var absent = await world.GetAsync(Missing, Annotator);

        var answers = new[]
        {
            await world.GetAsync(ScreenPaths.ReversalAsk, Annotator), await world.GetAsync(ScreenPaths.ReversalMine, Annotator),
            await world.GetAsync(ScreenPaths.ReversalMineOf(request), Annotator), await world.GetAsync(ScreenPaths.ReversalAuthorize, Annotator),
            await world.GetAsync(ScreenPaths.ReversalDecideOf(request), Annotator),
        };

        Assert.Equal(HttpStatusCode.NotFound, absent.Status);
        Assert.All(answers, answer => Assert.Equal((HttpStatusCode.NotFound, WithoutAddress(absent.Page)), (answer.Status, WithoutAddress(answer.Page))));
    }

    [DatabaseFact]
    public async Task AnnotatorPostingTheRequestIsDeniedByThePolicy()
    {
        await using var world = await StartAsync();
        var (item, marker) = await world.ItemAsync();
        var events = await world.ScalarAsync(CountEvents);
        var absent = await world.GetAsync(Missing, Annotator);

        var posted = await world.PostAsync(ScreenPaths.ReversalAsk, Draft(item, marker), Annotator);

        Assert.Equal((HttpStatusCode.NotFound, WithoutAddress(absent.Page)), (posted.Status, WithoutAddress(posted.Page)));
        Assert.Equal(("0", events), (await world.ScalarAsync(CountRequests), await world.ScalarAsync(CountEvents)));
    }

    [DatabaseFact]
    public async Task SentRequestShowsWhoIsMissingAndNoIdentity()
    {
        await using var world = await StartAsync();
        var (item, marker) = await world.ItemAsync();

        var sent = await world.PostAsync(ScreenPaths.ReversalAsk, Draft(item, marker));
        var page = await world.GetAsync(sent.Location ?? throw new InvalidOperationException("The answer leads nowhere."));
        var request = long.Parse(await world.ScalarAsync("SELECT max(id)::text FROM content.reversal_request"), CultureInfo.InvariantCulture);
        var later = await world.GetAsync(ScreenPaths.ReversalMineOf(request));

        Assert.Equal((HttpStatusCode.SeeOther, $"{ScreenPaths.ReversalMineOf(request)}?enviado"), (sent.Status, sent.Location));
        Assert.Contains($"<h1>Pedido {request} enviado</h1>", page.Page, StringComparison.Ordinal);
        Assert.Contains($"<h1>Pedido {request}</h1>", later.Page, StringComparison.Ordinal);
        Assert.Contains($"Aguarda autorização</span> Falta outra pessoa da Curadoria de {SubmissionWorld.NameOf(SubmissionWorld.Domain)}.", page.Page, StringComparison.Ordinal);
        Assert.Matches(Fact("Marcador", $"<code>{marker}</code>"), page.Page);
        Assert.Matches(Fact("Item", $"{item}"), page.Page);
        Assert.Matches(Fact("Prazo pedido", "30 dias"), page.Page);
        Assert.DoesNotContain("zqx-original-sintetico", page.Page, StringComparison.Ordinal);
        var recorded = await world.ScalarAsync(
            "SELECT string_agg(item_kind || '|' || item_id || '|' || coalesce(reference, ''), ',') FROM public.custody_event WHERE step = 'reversal_requested'");
        Assert.Contains($"item_text|{item}|", recorded, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task OwnRequestIsNotListedForApproval()
    {
        await using var world = await StartAsync();
        var (request, _) = await world.AskedAsync();

        var mine = await world.GetAsync(ScreenPaths.ReversalAuthorize);
        var theirs = await world.GetAsync(ScreenPaths.ReversalAuthorize, world.Second);
        var opened = await world.GetAsync(ScreenPaths.ReversalDecideOf(request));

        Assert.Contains("Nenhum pedido aguarda você.", mine.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(ScreenPaths.ReversalDecideOf(request), mine.Page, StringComparison.Ordinal);
        Assert.Contains("1 pedido de outra pessoa da Curadoria aguarda você.", theirs.Page, StringComparison.Ordinal);
        Assert.Contains($"href=\"{ScreenPaths.ReversalDecideOf(request)}\"", theirs.Page, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.ReversalMineOf(request)), (opened.Status, opened.Location));
    }

    [DatabaseFact]
    public async Task DirectPostOnOwnRequestIsRefused()
    {
        await using var world = await StartAsync();
        var (request, _) = await world.AskedAsync();

        var authorized = await world.PostAsync(ScreenPaths.ReversalDecideOf(request), Decision("autorizar", world.Tomorrow));
        var declined = await world.PostAsync(ScreenPaths.ReversalDecideOf(request), Decision("recusar", string.Empty));
        var page = await world.GetAsync(ScreenPaths.ReversalMineOf(request));

        Assert.All([authorized, declined], answer => Assert.Contains("Você não autoriza o próprio pedido.", answer.Page, StringComparison.Ordinal));
        Assert.Contains("Aguarda autorização", page.Page, StringComparison.Ordinal);
        Assert.Equal(
            ("0", "2", "0"),
            (await world.ScalarAsync(CountAuthorizations), await world.ScalarAsync("SELECT count(*)::text FROM public.custody_event WHERE reason = 'reversal_self'"),
                await world.ScalarAsync("SELECT count(*)::text FROM public.custody_event WHERE step = 'reversal_declined'")));
    }

    [DatabaseFact]
    public async Task PendingRequestShowsReasonUseAndTermAndNoIdentity()
    {
        await using var world = await StartAsync();
        var (request, item) = await world.AskedAsync();

        var page = await world.GetAsync(ScreenPaths.ReversalDecideOf(request), world.Second);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains($"<h1>Pedido {request}</h1>", page.Page, StringComparison.Ordinal);
        Assert.Contains($"</code> no item {item}</p>", page.Page, StringComparison.Ordinal);
        Assert.Matches(Fact("Por que precisa", "zqx-motivo-sintetico"), page.Page);
        Assert.Matches(Fact("O que fará", "zqx-uso-sintetico"), page.Page);
        Assert.Matches(Fact("Prazo pedido", "30 dias"), page.Page);
        Assert.Contains("zqx-curadora, em ", page.Page, StringComparison.Ordinal);
        var offered = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime).AddDays(30).ToString("O", CultureInfo.InvariantCulture);
        Assert.Contains($"value=\"{offered}\"", page.Page, StringComparison.Ordinal);
        Assert.DoesNotContain("zqx-original-sintetico", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ReadingTheRequestTextIsRecordedBeforeOpening()
    {
        await using var world = await StartAsync();
        var (request, _) = await world.AskedAsync();
        var before = await world.ScalarAsync("SELECT count(*)::text FROM public.custody_event WHERE step = 'reversal_request_read'");

        var page = await world.GetAsync(ScreenPaths.ReversalDecideOf(request), world.Second);

        Assert.Contains("zqx-motivo-sintetico", page.Page, StringComparison.Ordinal);
        Assert.Equal(
            ("0", $"reversal_request|{request}|reversal_request_read|administrative_act||{world.Second}|true"),
            (before, await world.ScalarAsync(LastEvent)));
    }

    [DatabaseFact]
    public async Task AuthorizationIsConfirmedOnItsOwnPageForThatRequestOnly()
    {
        await using var world = await StartAsync();
        var (request, _) = await world.AskedAsync();
        var (other, _) = await world.AskedAsync();
        var unconfirmed = await world.GetAsync($"{ScreenPaths.ReversalDecideOf(request)}?autorizado", world.Second);

        var past = await world.PostAsync(ScreenPaths.ReversalDecideOf(request), Decision("autorizar", "2020-01-01"), world.Second);
        var sent = await world.PostAsync(ScreenPaths.ReversalDecideOf(request), Decision("autorizar", world.Tomorrow), world.Second);
        var page = await world.GetAsync(sent.Location ?? throw new InvalidOperationException("The answer leads nowhere."), world.Second);

        Assert.Contains("Autorizar este pedido até", unconfirmed.Page, StringComparison.Ordinal);
        Assert.Matches(Error("validade", "Nada foi autorizado: escolha uma data futura."), past.Page);
        Assert.Equal((HttpStatusCode.SeeOther, $"{ScreenPaths.ReversalDecideOf(request)}?autorizado"), (sent.Status, sent.Location));
        Assert.Contains($"<h1>Pedido {request} autorizado</h1>", page.Page, StringComparison.Ordinal);
        Assert.Contains($"quem pediu pode revelar uma vez, até {world.TomorrowShown}.", page.Page, StringComparison.Ordinal);
        Assert.Contains($"Autorizado até {world.TomorrowShown}", (await world.GetAsync(ScreenPaths.ReversalMineOf(request))).Page, StringComparison.Ordinal);
        Assert.Contains("Aguarda autorização", (await world.GetAsync(ScreenPaths.ReversalMineOf(other))).Page, StringComparison.Ordinal);
        Assert.Equal(
            $"{request}|{world.Second}",
            await world.ScalarAsync("SELECT string_agg(request_id || '|' || authorized_by, ',') FROM content.reversal_authorization"));
    }

    [DatabaseFact]
    public async Task AuthorizationPastItsTermIsShownExpired()
    {
        await using var world = await StartAsync();
        var (request, _) = await world.AskedAsync();
        _ = await world.PostAsync(ScreenPaths.ReversalDecideOf(request), Decision("autorizar", world.Tomorrow), world.Second);
        var shown = world.TomorrowShown;

        world.Clock.Advance(TimeSpan.FromDays(3));
        var page = await world.GetAsync(ScreenPaths.ReversalMineOf(request));
        var list = await world.GetAsync(ScreenPaths.ReversalMine);
        var again = await world.GetAsync(ScreenPaths.ReversalAuthorize, world.Second);

        Assert.Contains($"a autorização venceu em {shown}.", page.Page, StringComparison.Ordinal);
        Assert.Matches(new Regex($"href=\"{ScreenPaths.ReversalMineOf(request)}\".*?Vencido", RegexOptions.Singleline, TimeSpan.FromSeconds(1)), list.Page);
        Assert.Contains($"href=\"{ScreenPaths.ReversalDecideOf(request)}\"", again.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DeclineIsRecordedWithWhoWhenAndScope()
    {
        await using var world = await StartAsync();
        var (request, item) = await world.AskedAsync();

        var sent = await world.PostAsync(ScreenPaths.ReversalDecideOf(request), Decision("recusar", string.Empty), world.Second);
        var recorded = await world.ScalarAsync(LastEvent);
        var page = await world.GetAsync(sent.Location ?? throw new InvalidOperationException("The answer leads nowhere."), world.Second);
        var mine = await world.GetAsync(ScreenPaths.ReversalMineOf(request));

        Assert.Equal((HttpStatusCode.SeeOther, $"{ScreenPaths.ReversalDecideOf(request)}?recusado"), (sent.Status, sent.Location));
        Assert.Equal($"reversal_request|{request}|reversal_declined|administrative_act||{world.Second}|true", recorded);
        Assert.Contains($"<h1>Pedido {request} recusado</h1>", page.Page, StringComparison.Ordinal);
        Assert.Contains("a recusa ficou na cadeia de custódia.", page.Page, StringComparison.Ordinal);
        Assert.Contains($"Recusado</span> <code>", mine.Page, StringComparison.Ordinal);
        Assert.Contains($"</code> no item {item}</p>", mine.Page, StringComparison.Ordinal);
        Assert.Equal("0", await world.ScalarAsync(CountAuthorizations));
    }

    [DatabaseFact]
    public async Task MissingOrOtherDomainRequestAnswersNotFound()
    {
        await using var world = await StartAsync();
        var (request, _) = await world.AskedAsync();
        var (absent, absentOther) = (await world.GetAsync(Missing), await world.GetAsync(Missing, world.Other));

        var answers = new[]
        {
            await world.GetAsync(ScreenPaths.ReversalMineOf(request + 100)), await world.GetAsync(ScreenPaths.ReversalDecideOf(request + 100), world.Second),
            await world.GetAsync(ScreenPaths.ReversalMineOf(request), world.Second),
        };
        var theirs = new[] { await world.GetAsync(ScreenPaths.ReversalMineOf(request), world.Other), await world.GetAsync(ScreenPaths.ReversalDecideOf(request), world.Other) };

        Assert.All(answers, answer => Assert.Equal((HttpStatusCode.NotFound, WithoutAddress(absent.Page)), (answer.Status, WithoutAddress(answer.Page))));
        Assert.All(theirs, answer => Assert.Equal((HttpStatusCode.NotFound, WithoutAddress(absentOther.Page)), (answer.Status, WithoutAddress(answer.Page))));
    }

    [DatabaseFact]
    public async Task ListFilterAndPageStayInTheAddress()
    {
        await using var world = await StartAsync();
        var asked = new List<(long Id, long Item)>();
        for (var made = 0; made <= ReversalPage.Size; made++)
        {
            asked.Add(await world.AskedAsync());
        }

        var first = await world.GetAsync(ScreenPaths.ReversalAuthorize, world.Second);
        var second = await world.GetAsync($"{ScreenPaths.ReversalAuthorize}?pagina=2", world.Second);
        var filtered = await world.GetAsync($"{ScreenPaths.ReversalAuthorize}?item={asked[3].Item}", world.Second);
        var pending = await world.GetAsync($"{ScreenPaths.ReversalMine}?situacao=pendente&pagina=2");
        var declined = await world.GetAsync($"{ScreenPaths.ReversalMine}?situacao=recusado");

        Assert.Contains($"{ReversalPage.Size + 1} pedidos de outras pessoas da Curadoria aguardam você.", first.Page, StringComparison.Ordinal);
        Assert.Contains($"href=\"{ScreenPaths.ReversalAuthorize}?pagina=2\"", first.Page, StringComparison.Ordinal);
        Assert.Equal(ReversalPage.Size, Links(first.Page, ScreenPaths.ReversalAuthorize));
        Assert.Equal(1, Links(second.Page, ScreenPaths.ReversalAuthorize));
        Assert.Contains($"href=\"{ScreenPaths.ReversalDecideOf(asked[0].Id)}\"", second.Page, StringComparison.Ordinal);
        Assert.Equal(1, Links(filtered.Page, ScreenPaths.ReversalAuthorize));
        Assert.Contains($"href=\"{ScreenPaths.ReversalDecideOf(asked[3].Id)}\"", filtered.Page, StringComparison.Ordinal);
        Assert.Contains($"value=\"{asked[3].Item}\"", filtered.Page, StringComparison.Ordinal);
        Assert.Equal(1, Links(pending.Page, ScreenPaths.ReversalMine));
        Assert.Contains($"href=\"{ScreenPaths.ReversalMine}?situacao=pendente&pagina=1\"", pending.Page, StringComparison.Ordinal);
        Assert.Contains("<option value=\"pendente\" selected", pending.Page, StringComparison.Ordinal);
        Assert.Equal(0, Links(declined.Page, ScreenPaths.ReversalMine));
    }

    private static Dictionary<string, string> Decision(string chosen, string until) =>
        new(StringComparer.Ordinal) { ["decisao"] = chosen, ["validade"] = until };

    private static int Links(string page, string under) =>
        Regex.Count(page, $"href=\"{Regex.Escape(under)}/[0-9]+\"", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>The page without the addresses its forms send to, which name the route asked; the rest of a page that is not there is the same for every route.</summary>
    private static string WithoutAddress(string page) => Regex.Replace(page, "action=\"[^\"]*\"", string.Empty, RegexOptions.None, TimeSpan.FromSeconds(1));

    private static Regex Error(string field, string words) =>
        new($"id=\"{field}\"[^>]*aria-invalid=\"true\"[\\s\\S]*?<p id=\"{field}-erro\" class=\"field__error\" role=\"alert\">{Regex.Escape(words)}</p>", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static Regex Fact(string term, string value) =>
        new($"<dt>{Regex.Escape(term)}</dt>\\s*<dd>{Regex.Escape(value)}</dd>", RegexOptions.None, TimeSpan.FromSeconds(1));
}
