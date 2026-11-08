using System.Net;
using System.Reflection;
using Core.Host.Screens;
using Core.Persistence.Ingestion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests;

/// <summary>
/// The pieces of the list of batches (#608), rendered as the server renders them, before any route: each batch with its state, the motive of a rejected one (CA-2) and what the
/// submission brought, and nothing of the material; the filter by state and period given back and kept in the address of the pages (CA-1); and the empty list of a person
/// with no grant, which says why and is no error (CA-3).
/// </summary>
public sealed class BatchesScreenTests
{
    private const string Digest = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private static readonly DateTimeOffset At = new(2026, 11, 8, 14, 30, 0, TimeSpan.Zero);

    private static readonly BatchesScreen.Row[] ThreeStates =
    [
        new(43, BatchState.Open, null, "zqx-ana", At, 2048, Digest),
        new(42, BatchState.Rejected, "invalid_rows", "zqx-bia", At.AddHours(-1), 1536, Digest),
        new(41, BatchState.Accepted, null, "zqx-caio", At.AddDays(-1), null, null),
    ];

    [Fact]
    public async Task EachBatchShowsItsStateWhoSubmittedWhenAndWhatTheSubmissionBroughtInATableThatStacksOnAPhone()
    {
        var page = await RenderAsync(new() { ["Rows"] = ThreeStates, ["Granted"] = true });

        Assert.Contains("class=\"data-table data-table--stacks\"", page, StringComparison.Ordinal);
        Assert.Contains("--data-table-header-6: \"SHA-256\";", page, StringComparison.Ordinal);
        Assert.Contains("Lote 43", page, StringComparison.Ordinal);
        Assert.Contains("Em curso", page, StringComparison.Ordinal);
        Assert.Contains("Aceito", page, StringComparison.Ordinal);
        Assert.Contains("zqx-bia", page, StringComparison.Ordinal);
        Assert.Contains("08/11/2026 às 14:30:00 UTC", page, StringComparison.Ordinal);
        Assert.Contains("2,0 KiB", page, StringComparison.Ordinal);
        Assert.Contains($"<code>{Digest}</code>", page, StringComparison.Ordinal);
        Assert.Contains("Sem envio registrado", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARejectedBatchIsListedWithItsStateAndTheMotiveInWords()
    {
        var page = await RenderAsync(new() { ["Rows"] = ThreeStates, ["Granted"] = true });

        Assert.Contains("mark--bad", page, StringComparison.Ordinal);
        Assert.Contains("Rejeitado", page, StringComparison.Ordinal);
        Assert.Contains("O arquivo tinha linhas que não puderam ser lidas.", page, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingTheListTakesOfABatchCanHoldAWordOfTheMaterial()
    {
        var taken = typeof(BatchesScreen.Row).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name);

        Assert.DoesNotContain(taken, name => name.Contains("Text", StringComparison.Ordinal) || name.Contains("Content", StringComparison.Ordinal) || name.Contains("Item", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APersonWithNoGrantSeesAnEmptyListThatSaysWhyAndNoTable()
    {
        var page = await RenderAsync(new() { ["Rows"] = Array.Empty<BatchesScreen.Row>(), ["Granted"] = false });

        Assert.Contains("Nenhum lote para mostrar.", page, StringComparison.Ordinal);
        Assert.Contains("Você não tem concessão em nenhum domínio.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", page, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"alert\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFilterThatLeavesNoBatchSaysSoAndHowToSeeTheOthers()
    {
        var page = await RenderAsync(new() { ["Rows"] = Array.Empty<BatchesScreen.Row>(), ["Granted"] = true, ["State"] = "rejeitados" });

        Assert.Contains("Nenhum lote com esse filtro.", page, StringComparison.Ordinal);
        Assert.Contains("Mude ou limpe o filtro para ver os outros.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFilterComesBackToItsFieldsAndTheAddressOfTheNextPageKeepsIt()
    {
        var page = await RenderAsync(new()
        {
            ["Rows"] = ThreeStates, ["Granted"] = true, ["State"] = "rejeitados", ["From"] = "2026-11-01", ["Until"] = "2026-11-08", ["HasNext"] = true,
        });

        Assert.Contains("name=\"estado\" value=\"rejeitados\" checked", page, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"\" checked", page, StringComparison.Ordinal);
        Assert.Contains("name=\"de\" type=\"date\" value=\"2026-11-01\"", page, StringComparison.Ordinal);
        Assert.Contains("href=\"?estado=rejeitados&de=2026-11-01&ate=2026-11-08&pagina=2\" rel=\"next\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("rel=\"prev\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithASinglePageThereIsNoPagerAndOnTheLastOnlyTheWayBack()
    {
        var single = await RenderAsync(new() { ["Rows"] = ThreeStates, ["Granted"] = true });
        var last = await RenderAsync(new() { ["Rows"] = ThreeStates, ["Granted"] = true, ["Page"] = 3 });

        Assert.DoesNotContain("class=\"pager\"", single, StringComparison.Ordinal);
        Assert.DoesNotContain("Anterior", single, StringComparison.Ordinal);
        Assert.Contains("href=\"?pagina=2\" rel=\"prev\"", last, StringComparison.Ordinal);
        Assert.Contains("Página 3, 20 por página", last, StringComparison.Ordinal);
        Assert.DoesNotContain("Próxima", last, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APeriodTheServerRefusedIsToldAtTheFieldOfTheLastDay()
    {
        var page = await RenderAsync(new() { ["Rows"] = ThreeStates, ["Granted"] = true, ["PeriodError"] = "zqx-erro-do-periodo" });

        Assert.Contains("id=\"filtro-ate-erro\" class=\"field__error\" role=\"alert\">zqx-erro-do-periodo", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("em-curso", BatchState.Open)]
    [InlineData("aceitos", BatchState.Accepted)]
    [InlineData("rejeitados", BatchState.Rejected)]
    [InlineData("", null)]
    [InlineData("Rejected", null)]
    [InlineData(null, null)]
    public void TheStateOfTheAddressIsOneTheFilterOffersOrNone(string? key, BatchState? state) =>
        Assert.Equal(state, BatchesScreen.StateOf(key));

    [Fact]
    public void ThePeriodCountsBothDaysWholeInUniversalTime()
    {
        var (from, until, error) = BatchesScreen.PeriodOf("2026-11-01", "2026-11-08");

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), from);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero), until);
        Assert.Null(error);
    }

    [Fact]
    public void ALastDayBeforeTheFirstLeavesThePeriodOutAndSaysWhy()
    {
        var (from, until, error) = BatchesScreen.PeriodOf("2026-11-08", "2026-11-01");

        Assert.Null(from);
        Assert.Null(until);
        Assert.Equal("A data final vem antes da inicial; o período ficou de fora.", error);
    }

    [Theory]
    [InlineData("08/11/2026")]
    [InlineData("2026-13-01")]
    [InlineData("amanhã")]
    public void ADayThatIsNotADateIsLeftOutWithNoError(string typed)
    {
        var (from, until, error) = BatchesScreen.PeriodOf(typed, null);

        Assert.Null(from);
        Assert.Null(until);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1,5 KiB")]
    [InlineData(10_485_760, "10,0 MiB")]
    public void TheSizeIsReadInTheLargestUnitItFills(long bytes, string read) =>
        Assert.Equal(read, BatchTexts.SizeOf(bytes));

    [Theory]
    [InlineData("write_failed", "A gravação do material falhou e nada ficou guardado.")]
    [InlineData("zqx_motivo_novo", "Motivo registrado: zqx_motivo_novo.")]
    public void AMotiveIsSaidInWordsAndOneTheScreenDoesNotKnowIsSaidByItsName(string motive, string said) =>
        Assert.Equal(said, BatchTexts.MotiveOf(motive));

    private static async Task<string> RenderAsync(Dictionary<string, object?> given)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<BatchesScreen>(ParameterView.FromDictionary(given))).ToHtmlString());
        return WebUtility.HtmlDecode(html);
    }
}
