using System.Net;
using System.Text.RegularExpressions;
using Core.Boundary.Text;
using Core.Host.Endpoints;
using Core.Host.Tests.Submissions;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// The screens of the rerun of the sanitization (issue 97) behind the real gates, over a database of their own: the items by version, the confirmation on a page of its own whose POST only queues
/// and leads to the progress, the progress in each of its phases, the batch after the rerun, and the answer of a route that does not exist to whoever is not of the curation.
/// </summary>
[Trait("Category", "Database")]
public sealed class RerunRoutesTests
{
    private const string Missing = "/zqx-nao-existe-9741";
    private const string ConfirmLink = $"href=\"{ScreenPaths.RerunConfirm}\"";

    private static readonly string Before = RerunWorld.Before.Version;
    private static readonly string Current = CompositionRoot.StepsOf().Version;

    [DatabaseFact]
    public async Task RerunScreenShowsItemsByVersion()
    {
        await using var world = await RerunScreensWorld.StartAsync();

        var page = await world.GetAsync(ScreenPaths.Rerun);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Matches(Fact("Versão em uso", $"<code>{Current}</code>"), page.Page);
        Assert.Matches(new Regex($">{Before}</th>\\s*<td role=\"cell\">2</td>", RegexOptions.None, TimeSpan.FromSeconds(1)), page.Page);
        Assert.Matches(new Regex($"{ConfirmLink}[^>]*>Reexecutar 2 itens</a>", RegexOptions.None, TimeSpan.FromSeconds(1)), page.Page);
    }

    [DatabaseFact]
    public async Task NothingToRerunSaysSoAndOffersNoButton()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        _ = await world.Rerun.RequestAsync(CompositionRoot.StepsOf());
        await world.ConcludeOneAsync();
        await world.ConcludeOneAsync();

        var page = await world.GetAsync(ScreenPaths.Rerun);
        var confirm = await world.GetAsync(ScreenPaths.RerunConfirm);

        Assert.Contains("Nada a reexecutar.", page.Page, StringComparison.Ordinal);
        Assert.Matches(Fact("Itens nela", "2 de 2"), page.Page);
        Assert.DoesNotContain(ConfirmLink, page.Page, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.Rerun), (confirm.Status, confirm.Location));
    }

    [DatabaseFact]
    public async Task RerunIsConfirmedOnItsOwnPage()
    {
        await using var world = await RerunScreensWorld.StartAsync();

        var page = await world.GetAsync(ScreenPaths.RerunConfirm);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("<h1>Confirmar a reexecução</h1>", page.Page, StringComparison.Ordinal);
        var asked = $"2 itens de zqx-domain-a em versão anterior entram na fila para voltar a ser sanitizados a partir do texto bruto, pela versão {Current}.";
        Assert.Contains(asked, page.Page, StringComparison.Ordinal);
        Assert.Contains($"method=\"post\" action=\"{ScreenPaths.RerunConfirm}\"", page.Page, StringComparison.Ordinal);
        Assert.Contains(">Pôr 2 itens na fila</button>", page.Page, StringComparison.Ordinal);
        Assert.Null((await world.SituationAsync()).OpenRerunId);
    }

    [DatabaseFact]
    public async Task ConfirmationQueuesWithoutTreatingAnyItem()
    {
        await using var world = await RerunScreensWorld.StartAsync();

        _ = await world.ConfirmAsync();

        var progress = await world.Rerun.ProgressAsync((await world.SituationAsync()).OpenRerunId!.Value);
        Assert.Equal((Current, 2L, 0L, 0L, 0L, 0L), (progress.TargetVersion, progress.Queued, progress.Held, progress.Identical, progress.Replaced, progress.Refused));
        Assert.Equal(Before, await world.Rerun.VersionAsync());
    }

    [DatabaseFact]
    public async Task ConfirmedRerunRedirectsToTheProgress()
    {
        await using var world = await RerunScreensWorld.StartAsync();

        var confirmed = await world.ConfirmAsync();

        var open = (await world.SituationAsync()).OpenRerunId!.Value;
        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.RerunProgressOf(open)), (confirmed.Status, confirmed.Location));
        Assert.Equal("Na fila", await world.PhaseAsync(open));
    }

    [DatabaseFact]
    public async Task SituationWithAnOpenRerunLinksToItAndOffersNoButton()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        var open = await world.Rerun.RequestAsync(CompositionRoot.StepsOf());

        var page = await world.GetAsync(ScreenPaths.Rerun);

        Assert.Contains($"Há uma reexecução aberta para a versão em uso. <a href=\"{ScreenPaths.RerunProgressOf(open)}\">Acompanhar a reexecução</a>", page.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(ConfirmLink, page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task SecondConfirmationLeadsToTheOpenRerun()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        var first = await world.ConfirmAsync();

        var second = await world.ConfirmAsync();

        var open = (await world.SituationAsync()).OpenRerunId!.Value;
        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.RerunProgressOf(open)), (second.Status, second.Location));
        Assert.Equal((first.Location, 2L), (second.Location, (await world.Rerun.ProgressAsync(open)).Queued));
    }

    [DatabaseFact]
    public async Task ConfirmationTheChainDidNotTakeComesBackOnThePageAndQueuesNothing()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        await world.Rerun.Submissions.Database.ExecuteAsSuperuserAsync(
            "CREATE FUNCTION public.zqx_request_conflicts() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $f$ "
            + "BEGIN RAISE EXCEPTION 'conflict' USING ERRCODE = '40001'; END $f$; "
            + "CREATE TRIGGER zqx_request_conflicts BEFORE INSERT ON public.custody_event FOR EACH ROW WHEN (NEW.step = 'sanitization_rerun') EXECUTE FUNCTION public.zqx_request_conflicts()");

        var refused = await world.ConfirmAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.Status);
        Assert.Contains("Os itens não foram postos na fila. Tente de novo em instantes", refused.Page, StringComparison.Ordinal);
        Assert.Null((await world.SituationAsync()).OpenRerunId);
    }

    [DatabaseFact]
    public async Task ProgressShowsQueuedRunningStoppedAndDone()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        var rerun = await world.Rerun.RequestAsync(CompositionRoot.StepsOf());
        var queued = await world.PhaseAsync(rerun);
        _ = await world.Rerun.Queue.TakeAsync(SubmissionWorld.Domain, Current, TestContext.Current.CancellationToken);
        var running = await world.PhaseAsync(rerun);
        world.Rerun.Clock.Advance(SanitizationReruns.Hold + TimeSpan.FromSeconds(1));
        var stopped = await world.PhaseAsync(rerun);
        await world.ConcludeOneAsync();
        await world.ConcludeOneAsync();

        var done = await world.GetAsync(ScreenPaths.RerunProgressOf(rerun));

        Assert.Equal(("Na fila", "Em andamento", "Parado"), (queued, running, stopped));
        Assert.Matches(Fact("Situação", "Concluída"), done.Page);
        Assert.Matches(Fact("Tratados", "2 de 2"), done.Page);
        Assert.Contains("2 itens tratados pela versão alvo.", done.Page, StringComparison.Ordinal);
        Assert.Matches(Fact("Versão alvo", $"<code>{Current}</code>"), done.Page);
        Assert.DoesNotContain(">Atualizar</a>", done.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task RerunWithNoItemConcludedWithinTheHoldIsShownStopped()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        var rerun = await world.Rerun.RequestAsync(CompositionRoot.StepsOf());
        world.Rerun.Clock.Advance(SanitizationReruns.Hold - TimeSpan.FromSeconds(1));
        var withinTheHold = await world.PhaseAsync(rerun);
        await world.ConcludeOneAsync();
        world.Rerun.Clock.Advance(SanitizationReruns.Hold - TimeSpan.FromSeconds(1));
        var withinTheHoldOfTheLast = await world.PhaseAsync(rerun);
        world.Rerun.Clock.Advance(TimeSpan.FromSeconds(2));

        var page = await world.GetAsync(ScreenPaths.RerunProgressOf(rerun));

        Assert.Equal(("Na fila", "Em andamento"), (withinTheHold, withinTheHoldOfTheLast));
        Assert.Matches(Fact("Situação", "Parado"), page.Page);
        Assert.Contains("Nenhum item foi concluído no prazo de uma posse.", page.Page, StringComparison.Ordinal);
        Assert.Contains($"href=\"{ScreenPaths.RerunProgressOf(rerun)}\"", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task OpenRerunWhoseTargetIsNoLongerCurrentIsShownSupersededAndNotTaken()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        var rerun = await world.Rerun.RequestAsync(new([new IdentifierStep(new TextBoundaries()), new DestinationsStep()]));
        await world.Rerun.StartAsync(RerunScreensWorld.InForce);
        await world.Rerun.IdleAsync();

        var page = await world.GetAsync(ScreenPaths.RerunProgressOf(rerun));

        Assert.Equal((2L, 0L), ((await world.Rerun.ProgressAsync(rerun)).Queued, (await world.Rerun.ProgressAsync(rerun)).Held));
        Assert.Matches(Fact("Situação", "Superada"), page.Page);
        Assert.Contains($"A versão em uso mudou; esta reexecução não anda mais. <a href=\"{ScreenPaths.Rerun}\">Pedir a reexecução pela versão em uso</a>", page.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(">Atualizar</a>", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DatabaseLostWhileRerunningKeepsTheItemsAlreadyTreated()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        var rerun = await world.Rerun.RequestAsync(CompositionRoot.StepsOf());
        var opened = 0;
        await world.Rerun.StartAsync(RerunScreensWorld.InForce, async token =>
        {
            if (Interlocked.Increment(ref opened) >= 2)
            {
                throw new Npgsql.NpgsqlException("zqx: the database is gone");
            }

            return await world.Rerun.OpenAsync(token);
        });
        await world.Rerun.IdleAsync();

        await world.Rerun.PastTheHoldAsync();

        var page = await world.GetAsync(ScreenPaths.RerunProgressOf(rerun));
        var progress = await world.Rerun.ProgressAsync(rerun);
        Assert.Equal((1L, 1L), (progress.Identical + progress.Replaced, progress.Queued + progress.Held));
        Assert.Matches(Fact("Situação", "Parado"), page.Page);
        Assert.Matches(Fact("Tratados", "1 de 2"), page.Page);
    }

    [DatabaseFact]
    public async Task ReviewAfterRerunShowsTheNewVersion()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        var batch = ScreenPaths.BatchOf(world.Rerun.Batch);
        var before = await world.GetAsync(batch);
        _ = await world.ConfirmAsync();
        await world.Rerun.StartAsync(RerunScreensWorld.InForce);
        await world.Rerun.IdleAsync();

        var after = await world.GetAsync(batch);

        Assert.Matches(Fact("Versão da lista de passos", Before), before.Page);
        Assert.Matches(Fact("Versão da lista de passos", Current), after.Page);
        Assert.Matches(Fact("Itens sanitizados", "2 de 2"), after.Page);
    }

    [DatabaseFact]
    public async Task RequestWhoseItemsAnotherRequestClosedIsShownSupersededEvenToTheVersionInForce()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        var rerun = await world.Rerun.RequestAsync(CompositionRoot.StepsOf());
        _ = await world.Rerun.RequestAsync(new([new IdentifierStep(new TextBoundaries()), new DestinationsStep()]));

        var page = await world.GetAsync(ScreenPaths.RerunProgressOf(rerun));

        var progress = await world.Rerun.ProgressAsync(rerun);
        Assert.Equal((Current, 2L, 0L), (progress.TargetVersion, progress.Superseded, progress.Queued + progress.Held));
        Assert.Matches(Fact("Situação", "Superada"), page.Page);
        Assert.Matches(Fact("Tratados", "0 de 2"), page.Page);
    }

    [DatabaseFact]
    public async Task ProgressOfARequestTheDomainDoesNotHaveAnswersNotFound()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        var open = await world.Rerun.RequestAsync(CompositionRoot.StepsOf());
        var absent = await world.GetAsync(Missing);

        var page = await world.GetAsync(ScreenPaths.RerunProgressOf(open + 1));

        Assert.Equal((HttpStatusCode.NotFound, WithoutAddress(absent.Page)), (page.Status, WithoutAddress(page.Page)));
    }

    [DatabaseFact]
    public async Task RerunWithoutCurationAnswersNotFound()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        var open = await world.Rerun.RequestAsync(CompositionRoot.StepsOf());
        var absent = await world.GetAsync(Missing, RerunScreensWorld.Annotator);

        var answers = new[]
        {
            await world.GetAsync(ScreenPaths.Rerun, RerunScreensWorld.Annotator),
            await world.GetAsync(ScreenPaths.RerunConfirm, RerunScreensWorld.Annotator),
            await world.GetAsync(ScreenPaths.RerunProgressOf(open), RerunScreensWorld.Annotator),
            await world.ConfirmAsync(RerunScreensWorld.Annotator),
        };

        Assert.Equal(HttpStatusCode.NotFound, absent.Status);
        Assert.All(answers, answer => Assert.Equal((HttpStatusCode.NotFound, WithoutAddress(absent.Page)), (answer.Status, WithoutAddress(answer.Page))));
        Assert.Equal(open, (await world.SituationAsync()).OpenRerunId);
        Assert.Equal(2L, (await world.Rerun.ProgressAsync(open)).Queued);
    }

    private static Regex Fact(string term, string value) =>
        new($"<dt>{Regex.Escape(term)}</dt>\\s*<dd>{Regex.Escape(value)}</dd>", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static string WithoutAddress(string page) => Regex.Replace(page, "action=\"[^\"]*\"", string.Empty, RegexOptions.None, TimeSpan.FromSeconds(1));
}
