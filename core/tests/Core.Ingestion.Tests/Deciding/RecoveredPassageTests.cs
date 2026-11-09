using Core.Boundary.Text;
using Core.Ingestion.Deciding;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests.Deciding;

/// <summary>
/// The offsets stay in code points from the boundary to the item decided, and cutting the text by them gives exactly the span the service found,
/// also over emoji and combining marks (issue 84, CA-5).
/// </summary>
public sealed class RecoveredPassageTests
{
    private static readonly TextBoundaries Boundaries = new();

    private static readonly Gen<string> Pieces = Gen.OneOf("a", "é", "e\u0301", "\U0001F600", "\U0001F468\u200D\U0001F469", "\U00020BB7", "\u0301", " ", "Z");

    private static readonly Gen<(string Text, int Start, int End)> Spans = Gen.Int[1, 12].SelectMany(count => Pieces.Array[count]).SelectMany(pieces =>
    {
        var text = string.Concat(pieces);
        var length = text.EnumerateRunes().Count();
        return Gen.Int[0, length].SelectMany(start => Gen.Int[start, length].Select(end => (text, start, end)));
    });

    [Theory]
    [InlineData("\U0001F600 fui para Santos", 11, 17, "Santos")]
    [InlineData("Jose\u0301 e Ana", 0, 5, "Jose\u0301")]
    [InlineData("\U0001F468\u200D\U0001F469 falou com Ana", 0, 3, "\U0001F468\u200D\U0001F469")]
    [InlineData("e\u0301e\u0301", 1, 2, "\u0301")]
    public void TheDecidedOccurrenceCutsTheTextToTheSpanTheServiceFound(string text, int start, int end, string expected)
    {
        var found = Found.Occurrence("LOCATION", "PROPN", "obl", start, end);
        var material = Found.Accepted(MaterialForm.Comment, Units.Message(text, "ana", null));

        var decided = DecidedMaterial.Decide(material, [Found.Analysis(found)]).Decided!.Of(0)[0];

        Assert.Equal((expected, start, end), (Boundaries.Slice(text, decided.Occurrence), decided.Occurrence.Start, decided.Occurrence.End));
    }

    [Fact]
    [Trait("Kind", "Property")]
    public void TheSpanRecoveredByTheDecidedOffsetsIsTheSpanOfTheCodePointsAtThoseOffsets() =>
        PropertyRunner.FromProcess().Check(Spans, generated =>
        {
            var (text, start, end) = generated;
            var material = Found.Accepted(MaterialForm.Comment, Units.Message(text, "ana", null));
            var found = Found.Occurrence("LOCATION", "PROPN", "obl", start, end);

            var decided = DecidedMaterial.Decide(material, [Found.Analysis(found)]).Decided!.Of(0)[0];

            Assert.Equal(CodePoints(text, start, end), Boundaries.Slice(text, decided.Occurrence));
        });

    private static string CodePoints(string text, int start, int end) => string.Concat(text.EnumerateRunes().Skip(start).Take(end - start));
}
