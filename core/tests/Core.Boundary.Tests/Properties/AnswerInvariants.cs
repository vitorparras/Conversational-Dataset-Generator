using System.Text.Json;
using Core.Boundary.Contracts;
using Core.Boundary.Http;
using Core.Tests.Properties.Harness;

namespace Core.Tests.Properties.Boundary;

/// <summary>
/// The invariants of the answers the core accepts: decisão 0011 (docs/decisoes/0011-protocolo-com-o-servico-de-linguagem.md),
/// kept by decisão 0018, measures offsets in code points of the text sent; requisito 009
/// (docs/requisitos/009-anotacao-de-trecho-estavel.md) refuses limits that fall outside the text, never resolving them to a neighbour.
/// </summary>
internal sealed class AnswerInvariants
{
    private readonly PropertyRunner runner;
    private readonly AnswerValidation validation;

    public AnswerInvariants(PropertyRunner runner, AnswerValidation validation)
    {
        this.runner = runner;
        this.validation = validation;
    }

    /// <summary>Decisão 0011: an answer whose every offset lies within the code points of the text is accepted whole, as sent.</summary>
    public void AnswerInsideTheTextIsAcceptedWhole() =>
        runner.Check(AnswerGenerators.Inside, generated =>
        {
            var validated = validation.Validate(RequestFor(generated), BodyOf(generated));

            Assert.Equal(generated.Occurrences, validated.Response?.Occurrences);
        });

    /// <summary>Requisito 009: one occurrence outside the text refuses the whole answer, even when it lies within the UTF-16 units.</summary>
    public void OneOccurrenceOutsideTheTextRefusesTheAnswer() =>
        runner.Check(AnswerGenerators.OneOutside, generated =>
        {
            var validated = validation.Validate(RequestFor(generated), BodyOf(generated));

            Assert.Equal(ValidationProblem.OffsetOutsideText, validated.Problem);
        });

    private static AnalysisRequest RequestFor(AnswerCase generated) =>
        new(generated.Text, "pt", "ZQX-REF-0024", ContractVersion.Current);

    private static byte[] BodyOf(AnswerCase generated) =>
        JsonSerializer.SerializeToUtf8Bytes(new AnalysisResponse(generated.Occurrences, "zqx-model-1"), ContractJson.Options);
}
