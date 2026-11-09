using System.Net;
using System.Text.RegularExpressions;
using Core.Host.Screens;
using Core.Persistence.Ingestion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests;

/// <summary>
/// The pieces of the detail of a batch (#609), rendered as the server renders them, before any route: the counts by form, by destination of a passage and by replica (CA-1), the earlier
/// batch that recognized a resubmission (CA-2), the loss declared when the form was accommodated (CA-3), and nothing of the material, which the model has no place for (CA-4).
/// </summary>
public sealed class BatchDetailScreenTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private static readonly BatchDetailScreen.Detail Accepted = new(7, "zqx-dominio-a", BatchState.Accepted, null, "zqx-ana", At, OriginDeclared: true)
    {
        Forms = new BatchDetailScreen.FormCount[] { new("conversation", 12), new("comment", 1) },
        Destinations = new BatchDetailScreen.DestinationCount[] { new("remove", 4), new("pseudonymize", 9) },
        Replicas = new BatchDetailScreen.ReplicaCount(3, 1),
    };

    [Fact]
    public async Task Ca1TheDetailCountsTheItemsByFormThePassagesByDestinationAndTheReplicas()
    {
        var page = await RenderAsync(Accepted);

        Assert.Matches(Fact("Conversa", "12 itens"), page);
        Assert.Matches(Fact("Comentário", "1 item"), page);
        Assert.Matches(Fact("Removidos", "4 trechos"), page);
        Assert.Matches(Fact("Pseudonimizados", "9 trechos"), page);
        Assert.Matches(Fact("Preservados", "0 trechos"), page);
        Assert.Matches(Fact("Mensagens marcadas como réplica", "3"), page);
        Assert.Matches(Fact("Primeiras ocorrências desse conteúdo", "1"), page);
    }

    [Fact]
    public async Task Ca2AResubmissionSaysWhichEarlierBatchRecognizedItWithAWayToIt()
    {
        var page = await RenderAsync(Accepted with { Recognized = new BatchDetailScreen.Recognition(3, "/lotes/3") });
        var fresh = await RenderAsync(Accepted);

        Assert.Contains("Reconhecido como reenvio do material do <a href=\"/lotes/3\">lote 3</a>.", page, StringComparison.Ordinal);
        Assert.Contains("Não é reenvio de um lote anterior.", fresh, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ca3AnAccommodationShowsTheFormsAndEveryLossDeclared()
    {
        var page = await RenderAsync(Accepted with { Accommodation = new BatchDetailScreen.Accommodated("conversation", "document", 12, new[] { "author", "instant" }) });
        var plain = await RenderAsync(Accepted);

        Assert.Contains("Perda declarada", page, StringComparison.Ordinal);
        Assert.Contains("O material veio como uma conversa e foi acomodado como um documento, em 12 itens.", page, StringComparison.Ordinal);
        Assert.Contains(BatchDetailTexts.LossOf("author"), page, StringComparison.Ordinal);
        Assert.Contains(BatchDetailTexts.LossOf("instant"), page, StringComparison.Ordinal);
        Assert.DoesNotContain("Perda declarada", plain, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("author")]
    [InlineData("instant")]
    [InlineData("replies_to")]
    [InlineData("passages")]
    [InlineData("thread")]
    public void EveryLossOfTheSetHasItsOwnWordsAndNoneIsSaidByItsCode(string code) =>
        Assert.DoesNotContain(code, BatchDetailTexts.LossOf(code), StringComparison.Ordinal);

    [Fact]
    public void ACodeTheScreenDoesNotKnowIsSaidByItsNameNeverAsAnError()
    {
        Assert.Equal("Perda registrada: zqx.", BatchDetailTexts.LossOf("zqx"));
        Assert.Equal("Forma zqx", BatchDetailTexts.FormNameOf("zqx"));
    }

    [Fact]
    public async Task ARejectedBatchHasNoItemSaysWhyAndKeepsItsMotive()
    {
        var page = await RenderAsync(new BatchDetailScreen.Detail(8, "zqx-dominio-a", BatchState.Rejected, "no_units", "zqx-bia", At, OriginDeclared: false));

        Assert.Contains("Nenhum item neste lote.", page, StringComparison.Ordinal);
        Assert.Contains(BatchTexts.MotiveOf("no_units"), page, StringComparison.Ordinal);
        Assert.Contains("Desconhecida, como declarado no envio", page, StringComparison.Ordinal);
        Assert.Matches(Fact("Removidos", "0 trechos"), page);
    }

    [Fact]
    public async Task Ca4TheDetailShowsNoWordOfTheMaterialOnlyItsCountsAndAWayBackToTheList()
    {
        var page = await RenderAsync(Accepted);

        Assert.Contains("Nenhum texto do material aparece aqui.", page, StringComparison.Ordinal);
        Assert.Contains("href=\"/lotes?estado=aceitos\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<textarea", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<input", page, StringComparison.Ordinal);
    }

    private static string Fact(string term, string value) => $@"<dt>\s*{Regex.Escape(term)}\s*</dt>\s*<dd>\s*{Regex.Escape(value)}\s*</dd>";

    private static async Task<string> RenderAsync(BatchDetailScreen.Detail batch)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        var given = new Dictionary<string, object?> { ["Batch"] = batch, ["ListAddress"] = "/lotes?estado=aceitos" };
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<BatchDetailScreen>(ParameterView.FromDictionary(given))).ToHtmlString());
        return WebUtility.HtmlDecode(html);
    }
}
