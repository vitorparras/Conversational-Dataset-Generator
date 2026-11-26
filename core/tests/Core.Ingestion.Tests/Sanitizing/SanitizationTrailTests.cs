using Core.Ingestion.Deciding;
using Core.Ingestion.Sanitizing;
using Core.Tests.Support.Harness;
using CsCheck;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>A position on the raw text translates to the sanitized one through the edits the steps returned, and back; what was removed no longer exists (issue 92).</summary>
public sealed class SanitizationTrailTests
{
    private static readonly int[] Families = [0x4E00, 0x1F600, 0x0300];

    private static readonly Gen<(string Raw, DecidedOccurrence[] Decided, int Start, int End)> Queries = Gen.Int[0, 2].Array[1, 12].SelectMany(families =>
    {
        var raw = string.Concat(families.Select((family, index) => char.ConvertFromUtf32(Families[family] + index)));
        var span = Gen.Int[0, families.Length].SelectMany(start => Gen.Int[start, families.Length].Select(end => (start, end)));
        var removed = Gen.Select(span, Gen.Bool, (cut, secret) => secret ? Credential(cut.start, cut.end) : Data(cut.start, cut.end));
        return Gen.Select(removed.Array[0, 4], span, (decided, query) => (raw, decided, query.start, query.end));
    });

    [Fact]
    [Trait("Kind", "Property")]
    public void TranslatedPositionResolvesToTheSameSpanOrFailsVisibly() =>
        PropertyRunner.FromProcess().Check(Queries, generated =>
        {
            var (raw, decided, start, end) = generated;
            var sanitized = Sanitized(raw, decided);

            var forward = sanitized.Trail.Forward(start, end);

            if (!forward.Exists)
            {
                Assert.Contains(sanitized.Report[0].Edits, edit => start < edit.End && edit.Start < end);
                return;
            }

            Assert.DoesNotContain(sanitized.Report[0].Edits, edit => start < edit.End && edit.Start < end);
            Assert.Equal(Cut(raw, start, end), Cut(sanitized.Text, forward.Start, forward.End));
            Assert.Equal((true, start, end), Resolved(sanitized.Trail.Back(forward.Start, forward.End)));
        });

    [Fact]
    public void RemovedSpanTranslatesToNoLongerExists()
    {
        const string raw = "ab QXZ7 cd Wvkt";

        var trail = Sanitized(raw, Data(3, 7)).Trail;

        Assert.False(trail.Forward(3, 7).Exists);
        Assert.False(trail.Forward(5, 6).Exists);
        Assert.False(trail.Forward(0, 4).Exists);
        Assert.False(trail.Forward(6, 9).Exists);
        Assert.Equal((true, 13, 17), Resolved(trail.Forward(11, 15)));
        Assert.Equal((true, 0, 3), Resolved(trail.Forward(0, 3)));
        Assert.Equal((true, 9, 10), Resolved(trail.Forward(7, 8)));
    }

    [Fact]
    public void TranslationIsComposedFromReturnedEdits()
    {
        const string raw = "a [dado] b QXZ7 c";
        var first = new FixedStep("primeiro", _ => [new SanitizationEdit(2, 8, SanitizationMarker.Data)]);
        var second = new FixedStep("segundo", _ => [new SanitizationEdit(11, 15, SanitizationMarker.Credential)]);

        var sanitized = Sanitized(Sanitizer(first, second), raw);

        Assert.Equal("a [dado] b [credencial] c", sanitized.Text);
        Assert.Equal([first.Returned.Single(), second.Returned.Single()], sanitized.Trail.Steps);
        Assert.False(sanitized.Trail.Forward(2, 8).Exists);
        Assert.False(sanitized.Trail.Forward(11, 15).Exists);
        Assert.Equal((true, 24, 25), Resolved(sanitized.Trail.Forward(16, 17)));
    }

    [Fact]
    public void ALaterStepSeesThePositionsOfTheTextTheEarlierOneLeft()
    {
        const string raw = "QXZ7KWP9 b Wvkt";
        var first = new FixedStep("primeiro", _ => [new SanitizationEdit(0, 8, SanitizationMarker.Data)]);
        StepInput? seen = null;
        var second = new FixedStep("segundo", input =>
        {
            seen = input;
            return [];
        });

        _ = Sanitized(Sanitizer(first, second), raw, Pseudonymized(11, 15), Data(2, 4));

        var input = Assert.IsType<StepInput>(seen);
        var carried = Assert.Single(input.Occurrences);
        Assert.Equal("[dado] b Wvkt", input.Text);
        Assert.Equal("Wvkt", Boundaries.Slice(input.Text, carried.Occurrence));
    }

    [Fact]
    public void SanitizedSpanTranslatesBackToTheRawSpan()
    {
        const string raw = "chave QXZ7 de Wvkt";

        var sanitized = Sanitized(raw, Credential(6, 10), Pseudonymized(14, 18));

        var pseudonym = Assert.Single(sanitized.Pseudonyms).Occurrence;
        var back = sanitized.Trail.Back(pseudonym.Start, pseudonym.End);
        Assert.Equal((true, 14, 18), Resolved(back));
        Assert.Equal("Wvkt", Cut(raw, back.Start, back.End));
        Assert.False(sanitized.Trail.Back(6, 18).Exists);
        Assert.False(sanitized.Trail.Back(10, 11).Exists);
    }

    [Fact]
    public void PositionsCountCodePointsWithEmojiAndCombiningMarks()
    {
        const string raw = "\U0001F600e\u0301 QX7 \U0001F468\u200D\U0001F469 Wvkt";

        var sanitized = Sanitized(raw, Data(4, 7), Pseudonymized(12, 16));

        var pseudonym = Assert.Single(sanitized.Pseudonyms).Occurrence;
        Assert.Equal("\U0001F600e\u0301 [dado] \U0001F468\u200D\U0001F469 Wvkt", sanitized.Text);
        Assert.Equal((16, 19), (sanitized.Trail.RawLength, sanitized.Trail.SanitizedLength));
        Assert.Equal((15, 19), (pseudonym.Start, pseudonym.End));
        Assert.Equal((true, 11, 14), Resolved(sanitized.Trail.Forward(8, 11)));
        Assert.Equal("\U0001F468\u200D\U0001F469", Cut(sanitized.Text, 11, 14));
        Assert.Equal((true, 0, 3), Resolved(sanitized.Trail.Back(0, 3)));
    }

    [Fact]
    public void APositionOutsideTheTextFailsVisibly()
    {
        var trail = Sanitized("ab QXZ7 cd", Data(3, 7)).Trail;

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => _ = trail.Forward(0, 11));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => _ = trail.Forward(-1, 2));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => _ = trail.Forward(4, 3));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => _ = trail.Back(0, 13));
        _ = Assert.Throws<InvalidOperationException>(() => _ = trail.Forward(3, 7).Start);
    }

    [Fact]
    public void TrailOfTheReportsHasTheLengthTheirEditsLeave()
    {
        var untouched = SanitizationTrail.Of(7, []);
        var edited = SanitizationTrail.Of(5, [new StepReport("primeiro", 1, [new SanitizationEdit(1, 3, SanitizationMarker.Data)])]);

        Assert.Equal((7, 7, 0), (untouched.RawLength, untouched.SanitizedLength, untouched.Steps.Count));
        Assert.Equal((5, 9, 1), (edited.RawLength, edited.SanitizedLength, edited.Steps.Count));
    }

    [Fact]
    public void TrailOfReportsThatCannotBeTextIsRefused()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SanitizationTrail.Of(-1, []));
        _ = Assert.Throws<ArgumentNullException>(() => SanitizationTrail.Of(5, null!));
        _ = Assert.Throws<ArgumentException>(() => SanitizationTrail.Of(5, [null!]));
    }

    private static (bool Exists, int Start, int End) Resolved(TrailSpan span) => (span.Exists, span.Start, span.End);

    [Fact]
    public void PositionsAfterARepairedEmojiCountCodePoints()
    {
        const string raw = "\u00F0\u0178\u02DC\u20AC QZX7KW fim";

        var sanitized = Sanitized(Sanitizer(new EncodingRepairStep(Boundaries), new DestinationsStep()), raw, Data(5, 11));

        var end = sanitized.Trail.Forward(12, 15);
        Assert.Equal("\U0001F600 [dado] fim", sanitized.Text);
        Assert.Equal((true, 9, 12), (end.Exists, end.Start, end.End));
    }

    [Fact]
    public void EditThatMisstatesHowManyCodePointsItWritesFailsTheStep()
    {
        var misstating = new FixedStep("mede-errado", _ => [new SanitizationEdit(0, 1, null, "\U0001F600", 2)]);

        var failure = Assert.Throws<SanitizationStepFailedException>(() => Sanitizer(misstating).Sanitize("abc", []));

        Assert.Equal("mede-errado", failure.Step);
    }
}
