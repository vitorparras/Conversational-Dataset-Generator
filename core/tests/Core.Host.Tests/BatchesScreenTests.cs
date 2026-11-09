using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Core.Host.Screens;
using Core.Ingestion;
using Core.Persistence.Ingestion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests;

/// <summary>
/// The pieces of the list of batches (#608), rendered as the server renders them, before any route: each batch with its domain, its state, the motive of a rejected one in words
/// (CA-2) and the receipt of the submission, and nothing of the material; the filter by state and period given back and kept in the address of the pages (CA-1); and the empty
/// list of a person with no grant, which says why, offers no filter and is no error (CA-3).
/// </summary>
public sealed class BatchesScreenTests
{
    private const string Digest = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
    private const string Unknown = "Motivo registrado:";

    /// <summary>The refusals of the gate of the submission (#75, <c>SubmissionRefusal</c>), which is not in this branch yet; the test reads the enumeration when it is.</summary>
    private static readonly string[] GateRefusals = ["too_large", "unknown_format", "format_not_confirmed", "unreadable"];

    private static readonly DateTimeOffset At = new(2026, 11, 8, 14, 30, 0, TimeSpan.Zero);

    private static readonly BatchesScreen.Row[] ThreeStates =
    [
        new(43, "zqx-dominio-a", BatchState.Open, null, "zqx-ana", At, new(2048, Digest, null)),
        new(42, "zqx-dominio-b", BatchState.Rejected, "no_units", "zqx-bia", At.AddHours(-1), new(1536, Digest, "name_with_path")),
        new(41, "zqx-dominio-a", BatchState.Accepted, null, "zqx-caio", At.AddDays(-1), null),
    ];

    /// <summary>Every motive a rejection can carry: the code of each refusal of the conference and of the gate, written as the submission writes it.</summary>
    public static TheoryData<string> Motives() => [.. AllMotives()];

    private static IEnumerable<string> AllMotives() => Enum.GetValues<RefusalCode>().Select(CodeOf).Concat(GateRefusals).Distinct();

    [Fact]
    public async Task EachBatchShowsItsDomainItsStateWhoSubmittedWhenAndTheReceiptInATableThatStacksOnAPhone()
    {
        var page = await RenderAsync(new Dictionary<string, object?> { ["Rows"] = ThreeStates, ["Granted"] = true });

        Assert.Contains("class=\"data-table data-table--stacks\"", page, StringComparison.Ordinal);
        Assert.Contains("--data-table-header-2: \"Domínio\";", page, StringComparison.Ordinal);
        Assert.Contains("--data-table-header-6: \"Recibo do envio\";", page, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"Lote 43</th>\s*<td role=""cell"">zqx-dominio-a</td>", RegexOptions.None, TimeSpan.FromSeconds(1)), page);
        Assert.Matches(new Regex(@"Lote 42</th>\s*<td role=""cell"">zqx-dominio-b</td>", RegexOptions.None, TimeSpan.FromSeconds(1)), page);
        Assert.Contains("Em curso", page, StringComparison.Ordinal);
        Assert.Contains("Aceito", page, StringComparison.Ordinal);
        Assert.Contains("zqx-bia", page, StringComparison.Ordinal);
        Assert.Contains("08/11/2026 às 14:30 UTC", page, StringComparison.Ordinal);
        Assert.Contains("2,0 KiB", page, StringComparison.Ordinal);
        Assert.Contains($"Resumo (SHA-256): <code class=\"digest\">{Digest}</code>", page, StringComparison.Ordinal);
        Assert.Contains("Sem envio registrado", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARejectedBatchIsListedWithItsStateAndTheMotiveInWords()
    {
        var page = await RenderAsync(new Dictionary<string, object?> { ["Rows"] = ThreeStates, ["Granted"] = true });

        Assert.Contains("mark--bad", page, StringComparison.Ordinal);
        Assert.Contains("Rejeitado", page, StringComparison.Ordinal);
        Assert.Contains("O arquivo não trazia nenhuma unidade de material.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheWarningTheSubmissionRecordedIsShownInWordsWithItsReceipt()
    {
        var page = await RenderAsync(new Dictionary<string, object?> { ["Rows"] = ThreeStates, ["Granted"] = true });

        Assert.Contains("O nome do arquivo trazia um caminho de pastas; ele foi descartado e o envio seguiu.", page, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingTheListTakesOfABatchCanHoldAWordOfTheMaterialOrTheNameOfTheFile()
    {
        var taken = typeof(BatchesScreen.Row).GetProperties().Concat(typeof(BatchesScreen.Receipt).GetProperties()).Select(property => property.Name);

        Assert.DoesNotContain(taken, name => name.Contains("Text", StringComparison.Ordinal) || name.Contains("Content", StringComparison.Ordinal)
            || name.Contains("Item", StringComparison.Ordinal) || name.Contains("File", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APersonWithNoGrantSeesAnEmptyListThatSaysWhyWithNoFilterNoTableAndNoPages()
    {
        var page = await RenderAsync(new Dictionary<string, object?> { ["Rows"] = Array.Empty<BatchesScreen.Row>(), ["Granted"] = false, ["Page"] = 2 });

        Assert.Contains("Nenhum lote para mostrar.", page, StringComparison.Ordinal);
        Assert.Contains("Você não tem concessão em nenhum domínio.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", page, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"pager\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"alert\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFilterThatLeavesNoBatchSaysSoAndHowToSeeTheOthers()
    {
        var page = await RenderAsync(new Dictionary<string, object?> { ["Rows"] = Array.Empty<BatchesScreen.Row>(), ["Granted"] = true, ["State"] = "rejeitados" });

        Assert.Contains("Nenhum lote com esse filtro.", page, StringComparison.Ordinal);
        Assert.Contains("Mude ou limpe o filtro para ver os outros.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFilterComesBackToItsFieldsAndTheAddressOfTheNextPageKeepsIt()
    {
        var page = await RenderAsync(new Dictionary<string, object?>
        {
            ["Rows"] = ThreeStates,
            ["Granted"] = true,
            ["State"] = "rejeitados",
            ["From"] = "2026-11-01",
            ["Until"] = "2026-11-08",
            ["HasNext"] = true,
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
        var single = await RenderAsync(new Dictionary<string, object?> { ["Rows"] = ThreeStates, ["Granted"] = true });
        var last = await RenderAsync(new Dictionary<string, object?> { ["Rows"] = ThreeStates, ["Granted"] = true, ["Page"] = 3 });

        Assert.DoesNotContain("class=\"pager\"", single, StringComparison.Ordinal);
        Assert.DoesNotContain("Anterior", single, StringComparison.Ordinal);
        Assert.Contains("href=\"?pagina=2\" rel=\"prev\"", last, StringComparison.Ordinal);
        Assert.Contains("Página 3, 20 por página", last, StringComparison.Ordinal);
        Assert.DoesNotContain("Próxima", last, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhatTheServerLeftOutOfThePeriodIsToldAtTheFieldItCameFrom()
    {
        var page = await RenderAsync(new Dictionary<string, object?> { ["Rows"] = ThreeStates, ["Granted"] = true, ["FromError"] = "zqx-erro-de", ["UntilError"] = "zqx-erro-ate" });

        Assert.Contains("id=\"filtro-de-erro\" class=\"field__error\" role=\"alert\">zqx-erro-de", page, StringComparison.Ordinal);
        Assert.Contains("id=\"filtro-ate-erro\" class=\"field__error\" role=\"alert\">zqx-erro-ate", page, StringComparison.Ordinal);
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
        var (from, until, fromError, untilError) = BatchesScreen.PeriodOf("2026-11-01", "2026-11-08");

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), from);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero), until);
        Assert.Null(fromError);
        Assert.Null(untilError);
    }

    [Fact]
    public void ALastDayBeforeTheFirstLeavesThePeriodOutAndSaysWhyAtTheLast()
    {
        var (from, until, fromError, untilError) = BatchesScreen.PeriodOf("2026-11-08", "2026-11-01");

        Assert.Null(from);
        Assert.Null(until);
        Assert.Null(fromError);
        Assert.Equal("A data final vem antes da inicial; o período ficou de fora.", untilError);
    }

    [Theory]
    [InlineData("08/11/2026")]
    [InlineData("2026-13-01")]
    [InlineData("amanhã")]
    public void ADayThatIsNotADateIsLeftOutAndSaysSoAtItsField(string typed)
    {
        var (from, until, fromError, untilError) = BatchesScreen.PeriodOf(typed, "2026-11-08");

        Assert.Null(from);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero), until);
        Assert.Equal("Escreva a data como dia, mês e ano; ela ficou de fora do filtro.", fromError);
        Assert.Null(untilError);
    }

    [Fact]
    public void AFieldLeftEmptyIsNoPeriodAndNoError()
    {
        var (from, until, fromError, untilError) = BatchesScreen.PeriodOf(string.Empty, null);

        Assert.Null(from);
        Assert.Null(until);
        Assert.Null(fromError);
        Assert.Null(untilError);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1,5 KiB")]
    [InlineData(10_485_760, "10,0 MiB")]
    public void TheSizeIsReadInTheLargestUnitItFills(long bytes, string read) =>
        Assert.Equal(read, BatchTexts.SizeOf(bytes));

    [Theory]
    [MemberData(nameof(Motives))]
    public void EveryMotiveARejectionCanCarryHasWordsOfItsOwn(string motive) =>
        Assert.DoesNotContain(Unknown, BatchTexts.MotiveOf(motive), StringComparison.Ordinal);

    [Fact]
    public void NoTwoMotivesShareTheirWords()
    {
        var said = AllMotives().Select(BatchTexts.MotiveOf).ToList();

        Assert.Equal(said.Count, said.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AMotiveOrAWarningTheScreenDoesNotKnowIsSaidByItsName()
    {
        Assert.Equal("Motivo registrado: zqx_motivo_novo.", BatchTexts.MotiveOf("zqx_motivo_novo"));
        Assert.Equal("Aviso registrado: zqx_aviso_novo.", BatchTexts.WarningOf("zqx_aviso_novo"));
    }

    /// <summary>The code of a value of an enumeration as the submission writes it into the motive (<c>ReasonCodes.Of</c> of #75): <c>no_units</c> for <c>NoUnits</c>.</summary>
    private static string CodeOf<TValue>(TValue value)
        where TValue : struct, Enum
    {
        var code = new StringBuilder();
        foreach (var letter in value.ToString())
        {
            if (char.IsAsciiLetterUpper(letter) && code.Length > 0)
            {
                _ = code.Append('_');
            }

            _ = code.Append(char.IsAsciiLetterUpper(letter) ? (char)(letter + ('a' - 'A')) : letter);
        }

        return code.ToString();
    }

    private static async Task<string> RenderAsync(IDictionary<string, object?> given)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<BatchesScreen>(ParameterView.FromDictionary(given))).ToHtmlString());
        return WebUtility.HtmlDecode(html);
    }
}
