using Core.Boundary.Contracts;
using Core.Tests.Support.Harness;

namespace Core.Boundary.Tests.Properties;

/// <summary>
/// The invariants of the offsets the language service sends: decisão 0011
/// (docs/decisoes/0011-protocolo-com-o-servico-de-linguagem.md), kept by decisão 0018, measures them in code points and
/// has the core convert them; requisito 009 (docs/requisitos/009-anotacao-de-trecho-estavel.md) wants the span to
/// resolve to exactly the text analysed, or to fail visibly.
/// </summary>
internal sealed class OffsetInvariants
{
    private readonly PropertyRunner runner;
    private readonly OffsetConversion conversion;

    public OffsetInvariants(PropertyRunner runner, OffsetConversion conversion)
    {
        this.runner = runner;
        this.conversion = conversion;
    }

    /// <summary>Decisão 0011: the core converts the offsets it receives, so a code point offset taken to a UTF-16 index and back is the offset it was.</summary>
    public void CodePointOffsetSurvivesTheRoundTrip() =>
        runner.Check(TextGenerators.CodePointOffsets, generated =>
        {
            var index = conversion.ToUtf16Index(generated.Text, generated.Offset);

            var back = conversion.ToCodePointOffset(generated.Text, index);

            Assert.Equal(generated.Offset, back);
        });

    /// <summary>Requisito 009: the span recovered from the limits is, code point by code point, the text the service analysed.</summary>
    public void SliceIsTheSpanTheServiceAnalysed() =>
        runner.Check(TextGenerators.Spans, generated =>
        {
            var analysed = generated.Text.EnumerateRunes().Skip(generated.Start).Take(generated.End - generated.Start);

            var slice = conversion.Slice(generated.Text, new Occurrence(generated.Start, generated.End, "PERSON", "PROPN", "obl"));

            Assert.Equal(analysed, slice.EnumerateRunes());
        });

    /// <summary>Requisito 009: a position inside a character fails visibly; an index between the halves of a pair is refused, never rounded.</summary>
    public void IndexInsideAPairIsRefused() =>
        runner.Check(TextGenerators.IndexesInsidePairs, generated =>
            Assert.Throws<ArgumentOutOfRangeException>(() => conversion.ToCodePointOffset(generated.Text, generated.Offset)));
}
