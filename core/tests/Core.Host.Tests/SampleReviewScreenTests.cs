using System.Net;
using Core.Host.Screens;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests;

/// <summary>
/// The pieces of the review by sample (#610), rendered as the server renders them, before any route: the sample drawn, in its order and with no way to choose, swap or skip an item
/// (CA-1); the rejection of the whole batch on a page of its own that says no item will enter and records the kind found, never the data (CA-2); and the approval, offered only with
/// every item reviewed, and said with who, when and which sample once recorded (CA-3, CA-4).
/// </summary>
public sealed class SampleReviewScreenTests
{
    private const string Action = "/lotes/7/revisao";

    private static readonly DateTimeOffset At = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ca1TheSampleIsShownInTheOrderOfTheDrawWithItsDeclaredSizeAndOnlyTheNextItemCanBeMarked()
    {
        var page = await ReviewAsync(Drawn(reviewed: 1));

        Assert.Contains("Item 1 de 3", page, StringComparison.Ordinal);
        Assert.Contains("Item 3 de 3", page, StringComparison.Ordinal);
        Assert.Contains("3 itens", page, StringComparison.Ordinal);
        Assert.Contains("1 de 3", page, StringComparison.Ordinal);
        Assert.Contains($"action=\"{Action}/itens/2/revisado\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain($"{Action}/itens/1/revisado", page, StringComparison.Ordinal);
        Assert.DoesNotContain($"{Action}/itens/3/revisado", page, StringComparison.Ordinal);
        Assert.Contains("Revise os itens anteriores primeiro.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca1NothingOnTheScreenChoosesSwapsOrSkipsAnItem()
    {
        var page = await ReviewAsync(Drawn(reviewed: 0));

        Assert.DoesNotContain("<select", page, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"checkbox\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("pagina", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Pular", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Trocar", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachItemShowsTheMostTreatedVersionThatExistsAndSaysWhichItIs()
    {
        var page = await ReviewAsync(Drawn(reviewed: 0));

        Assert.Contains("zqx-texto-sanitizado-1", page, StringComparison.Ordinal);
        Assert.Contains(SampleTexts.VersionOf(sanitized: true), page, StringComparison.Ordinal);
        Assert.Contains(SampleTexts.VersionOf(sanitized: false), page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Ca4WithTheSampleNotAllReviewedTheScreenDoesNotOfferToApprove(int reviewed)
    {
        var page = await ReviewAsync(Drawn(reviewed));

        Assert.DoesNotContain($"{Action}/aprovar", page, StringComparison.Ordinal);
        Assert.Contains("Aprovar ainda não é possível.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca4AnEmptySampleOffersNoApprovalEitherAndSaysWhy()
    {
        var page = await ReviewAsync(new SampleReviewScreen.Drawn(7, "zqx-dominio", 3, [], null));

        Assert.DoesNotContain($"{Action}/aprovar", page, StringComparison.Ordinal);
        Assert.Contains("A amostra não tem item.", page, StringComparison.Ordinal);
        Assert.Contains("O lote não tem nenhum item gravado para sortear", page, StringComparison.Ordinal);
        Assert.DoesNotContain("quando todos os itens da amostra estiverem revisados", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<ol", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABatchWithFewerItemsThanTheDeclaredSizeSaysTheSampleIsTheWholeBatch()
    {
        var smaller = await ReviewAsync(Drawn(reviewed: 0) with { Declared = 10 });
        var full = await ReviewAsync(Drawn(reviewed: 0));

        Assert.Contains("O lote tem menos itens que o tamanho declarado, e por isso a amostra são todos os 3 itens dele.", smaller, StringComparison.Ordinal);
        Assert.DoesNotContain("menos itens que o tamanho declarado", full, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Ca2AFindingCanBeToldAtEveryItemReviewedOrNotWhileTheBatchIsUndecided(int reviewed)
    {
        var page = await ReviewAsync(Drawn(reviewed));

        Assert.All(Enumerable.Range(1, 3), position => Assert.Contains($"href=\"{Action}/itens/{position}/achado\"", page, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ca2ARejectedBatchSaysWhoWhenTheKindAndTheItemAndOffersNothingToDo()
    {
        var page = await ReviewAsync(Drawn(reviewed: 1) with { Rejected = new("zqx-curadora", At, 2, "documento") });

        Assert.Contains("Lote reprovado por zqx-curadora em", page, StringComparison.Ordinal);
        Assert.Contains("Número de documento no item 2 da amostra. Nenhum item dele entrará no acervo.", page, StringComparison.Ordinal);
        Assert.Contains("O lote já foi reprovado.", page, StringComparison.Ordinal);
        Assert.Contains("Não revisado: o lote já foi decidido.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("/achado", page, StringComparison.Ordinal);
        Assert.DoesNotContain("/revisado", page, StringComparison.Ordinal);
        Assert.DoesNotContain($"{Action}/aprovar", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca3AnApprovedBatchOffersNoFindingAnymore()
    {
        var page = await ReviewAsync(Drawn(reviewed: 3) with { Approved = new("zqx-curadora", At, "amostra-7-1") });

        Assert.DoesNotContain("/achado", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca3WithEveryItemReviewedTheApprovalIsOfferedAndOnceRecordedSaysWhoWhenAndWhichSample()
    {
        var offered = await ReviewAsync(Drawn(reviewed: 3));
        var recorded = await ReviewAsync(Drawn(reviewed: 3) with { Approved = new("zqx-curadora", At, "amostra-7-1") });

        Assert.Contains($"action=\"{Action}/aprovar\"", offered, StringComparison.Ordinal);
        Assert.Contains("Aprovar o lote 7", offered, StringComparison.Ordinal);
        Assert.Contains("Lote aprovado por zqx-curadora em", recorded, StringComparison.Ordinal);
        Assert.Contains("com a amostra amostra-7-1 revisada", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain($"{Action}/aprovar", recorded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca2AFindingLeadsToThePageOfItsOwnThatRejectsTheWholeBatch()
    {
        var review = await ReviewAsync(Drawn(reviewed: 1));
        var reject = await RejectAsync(error: null);

        Assert.Contains($"href=\"{Action}/itens/2/achado\"", review, StringComparison.Ordinal);
        Assert.Contains("nenhum item dele entrará no acervo, nem os que não estão na amostra", reject, StringComparison.Ordinal);
        Assert.Contains("O registro guarda quem revisou, o que foi achado, em qual item da amostra e quando.", reject, StringComparison.Ordinal);
        Assert.Contains("Reprovar o lote inteiro", reject, StringComparison.Ordinal);
        Assert.Contains($"href=\"{Action}\"", reject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca2TheFindingIsAKindToChooseNeverTheDataFoundAndARefusalIsToldAtTheGroup()
    {
        var reject = await RejectAsync(error: "Escolha o que foi achado.");

        Assert.All(SampleTexts.Findings, kind => Assert.Contains($"value=\"{kind.Value}\"", reject, StringComparison.Ordinal));
        Assert.DoesNotContain("<textarea", reject, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"text\"", reject, StringComparison.Ordinal);
        Assert.Contains("id=\"achado-erro\" class=\"field__error\" role=\"alert\">Escolha o que foi achado.", reject, StringComparison.Ordinal);
    }

    [Fact]
    public void AKindTheScreenDoesNotKnowIsSaidByItsName()
    {
        Assert.Equal("Nome de pessoa", SampleTexts.FindingOf("nome"));
        Assert.Equal("Achado registrado: zqx", SampleTexts.FindingOf("zqx"));
    }

    private static SampleReviewScreen.Drawn Drawn(int reviewed) => new(
        7,
        "zqx-dominio",
        3,
        [
            new(1, "zqx-texto-sanitizado-1", Sanitized: true, Reviewed: reviewed >= 1),
            new(2, "zqx-texto-bruto-2", Sanitized: false, Reviewed: reviewed >= 2),
            new(3, "zqx-texto-sanitizado-3", Sanitized: true, Reviewed: reviewed >= 3),
        ],
        null);

    private static Task<string> ReviewAsync(SampleReviewScreen.Drawn sample) =>
        RenderAsync<SampleReviewScreen>(new Dictionary<string, object?> { ["Sample"] = sample, ["Action"] = Action });

    private static Task<string> RejectAsync(string? error) => RenderAsync<SampleRejectScreen>(new Dictionary<string, object?>
    {
        ["Batch"] = 7L, ["Position"] = 2, ["Action"] = $"{Action}/itens/2/achado", ["Back"] = Action, ["Error"] = error,
    });

    private static async Task<string> RenderAsync<TScreen>(IDictionary<string, object?> given)
        where TScreen : IComponent
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<TScreen>(ParameterView.FromDictionary(given))).ToHtmlString());
        return WebUtility.HtmlDecode(html);
    }
}
