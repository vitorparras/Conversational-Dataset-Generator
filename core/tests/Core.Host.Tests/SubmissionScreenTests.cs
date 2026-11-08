using System.Net;
using Core.Host.Screens;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests;

/// <summary>
/// The pieces of the screen of the submission (#607), rendered as the server renders them, before any route: the form with the condition required and the most
/// restrictive chosen (CA-1), the error bound to its field, the refusal and the warning by fixed words (CA-2), the closed gate with no form (CA-3) and the way to
/// the batch opened (CA-4). Nothing a piece takes can hold the name of a file or a word of the material.
/// </summary>
public sealed class SubmissionScreenTests
{
    private const string Action = "/zqx-envio-6070";
    private const string BatchAddress = "/zqx-lote-6070/41";
    private const string Digest = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private static readonly Dictionary<string, object?> Nothing = [];

    private static readonly SubmissionForm.Choices Offer = new(
        [new ChoiceGroup.Option("7", "zqx-dominio")],
        ["cdg-conversa/1"],
        ["zqx_restrita", "zqx_media", "zqx_aberta"],
        10_485_760);

    [Fact]
    public async Task TheFormChoosesTheMostRestrictiveConditionAndSendsTheFileWhole()
    {
        var page = await RenderAsync(Nothing);

        Assert.Contains("enctype=\"multipart/form-data\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"condition\" value=\"zqx_restrita\" checked", page, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"zqx_media\" checked", page, StringComparison.Ordinal);
        Assert.Contains("zqx_restrita (a mais restrita)", page, StringComparison.Ordinal);
        Assert.Contains("zqx_aberta (a mais permissiva)", page, StringComparison.Ordinal);
        Assert.Contains("type=\"file\"", page, StringComparison.Ordinal);
        Assert.Contains("Até 10 MiB.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"alert\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFieldRefusedCarriesItsErrorAndTheFormGivesBackWhatWasChosen()
    {
        var page = await RenderAsync(new Dictionary<string, object?>
        {
            ["Errors"] = new Dictionary<string, string> { ["condition"] = "missing", ["origin"] = "conflict" },
            ["Given"] = new SubmissionForm.Typed("7", "comment", "cdg-conversa/1", "zqx_media", "zqx-origem-digitada", true),
        });

        Assert.Contains("nada foi gravado", page, StringComparison.Ordinal);
        Assert.Contains("id=\"condition-erro\"", page, StringComparison.Ordinal);
        Assert.Contains(SubmissionTexts.FieldErrorOf("condition", "missing"), page, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"origem-dica origem-erro\"", page, StringComparison.Ordinal);
        Assert.Contains(SubmissionTexts.FieldErrorOf("origin", "conflict"), page, StringComparison.Ordinal);
        Assert.Contains("value=\"zqx_media\" checked", page, StringComparison.Ordinal);
        Assert.Contains("value=\"comment\" checked", page, StringComparison.Ordinal);
        Assert.Contains("value=\"zqx-origem-digitada\"", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("recovery_not_rehearsed")]
    [InlineData("recovery_rehearsal_stale")]
    public async Task TheClosedGateSaysWhyAndOffersNoSending(string code)
    {
        var page = await RenderAsync(new Dictionary<string, object?> { ["BlockedBy"] = code });

        Assert.Contains(SubmissionTexts.BlockedOf(code), page, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", page, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"file\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAcceptedSubmissionLeadsToItsBatchAndSaysTheNameWasDropped()
    {
        var answer = new SubmissionAnswer.Result(41, BatchAddress, 2_048, Digest, true, null, [], "name_with_path");

        var page = await RenderAsync(new Dictionary<string, object?> { ["Answer"] = answer });

        Assert.Contains($"id=\"abrir-lote\" class=\"button button--primary\" href=\"{BatchAddress}\"", page, StringComparison.Ordinal);
        Assert.Contains("o lote 41 está aberto", page, StringComparison.Ordinal);
        Assert.Contains(SubmissionTexts.WarningOf("name_with_path"), page, StringComparison.Ordinal);
        Assert.Contains(Digest, page, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARejectedSubmissionSaysTheReasonByItsCodeAndWhereToCorrect()
    {
        var points = Enumerable.Range(0, 12).Select(unit => new SubmissionAnswer.Point(unit, "author")).ToList();
        var answer = new SubmissionAnswer.Result(42, BatchAddress, 512, Digest, false, "field_missing", points, null);

        var page = await RenderAsync(new Dictionary<string, object?> { ["Answer"] = answer });

        Assert.Contains(SubmissionTexts.ReasonOf("field_missing"), page, StringComparison.Ordinal);
        Assert.Contains("Na unidade 10, campo autor", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Na unidade 11,", page, StringComparison.Ordinal);
        Assert.Contains("E mais 2 pontos", page, StringComparison.Ordinal);
        Assert.Contains($"id=\"enviar-de-novo\" class=\"button button--primary\" href=\"{Action}\"", page, StringComparison.Ordinal);
        Assert.Contains("42, recusado e registrado", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASendingNotRecordedOpensTheFormAgainWithWhatToDo()
    {
        var page = await RenderAsync(new Dictionary<string, object?> { ["NotRecorded"] = true });

        Assert.Contains(SubmissionTexts.NotRecorded, page, StringComparison.Ordinal);
        Assert.Contains("<form", page, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingThePiecesTakeCanHoldTheNameOfAFileOrAWordOfTheMaterial()
    {
        var taken = new[] { typeof(SubmissionScreen), typeof(SubmissionForm), typeof(SubmissionAnswer), typeof(SubmissionAnswer.Result), typeof(SubmissionAnswer.Point), typeof(SubmissionForm.Typed) }
            .SelectMany(type => type.GetProperties())
            .Select(property => property.Name);

        Assert.DoesNotContain(taken, name => name.Contains("Name", StringComparison.Ordinal) || name.Contains("Text", StringComparison.Ordinal) || name.Contains("Content", StringComparison.Ordinal));
    }

    private static async Task<string> RenderAsync(IReadOnlyDictionary<string, object?> given)
    {
        var parameters = new Dictionary<string, object?>(given) { ["Action"] = Action, ["Offer"] = Offer };
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<SubmissionScreen>(ParameterView.FromDictionary(parameters))).ToHtmlString());
        return WebUtility.HtmlDecode(html);
    }
}
