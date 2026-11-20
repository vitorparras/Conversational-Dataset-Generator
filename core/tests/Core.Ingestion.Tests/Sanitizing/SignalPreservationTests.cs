using Core.Ingestion.Deciding;
using Core.Ingestion.Sanitizing;
using Core.Tests.Support.Harness;
using CsCheck;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;
using static Core.Tests.Support.Harness.SignalTexts;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>
/// The marks of how people write survive every registered step: outside the edits that removed something, the text reappears character by character, in order, and a step that
/// evens out a mark fails even when its scope declares it (issue 95).
/// </summary>
public sealed class SignalPreservationTests
{
    private const string Identifier = "QZX7KW";

    /// <summary>Signal, an identifier marked for removal, and signal again.</summary>
    private static readonly Gen<(SignalSample SignalSample, DecidedOccurrence Removed)> AroundARemoval = Gen.Select(Of([.. Kinds]), Of([.. Kinds]), Gen.Bool, (before, after, secret) =>
    {
        var start = before.Marks[^1].End + 1;
        var end = start + Identifier.Length;
        var shifted = after.Marks.Select(mark => mark with { Start = mark.Start + end + 1, End = mark.End + end + 1 });
        var sample = new SignalSample($"{before.Text} {Identifier} {after.Text}", [.. before.Marks, .. shifted]);
        return (sample, secret ? Credential(start, end) : Data(start, end));
    });

    private static Sanitizer EveryStep(params ISanitizationStep[] more) => Sanitizer([.. StepConformance.Registered(), .. more]);

    /// <summary>The marks that do not come out as they were written, where the sanitized text has them.</summary>
    private static List<SignalMark> Lost(Sanitizer sanitizer, SignalSample sample, params DecidedOccurrence[] decided)
    {
        var sanitized = Sanitized(sanitizer, sample.Text, decided);
        return [.. sample.Marks.Where(mark => !Survives(sanitized, mark))];
    }

    private static bool Survives(SanitizedText sanitized, SignalMark mark)
    {
        var span = sanitized.Trail.Forward(mark.Start, mark.End);
        return span.Exists && Cut(sanitized.Text, span.Start, span.End) == mark.Text;
    }

    [Fact]
    [Trait("Kind", "Property")]
    public void SlangMisspellingRepetitionAndExpressivePunctuationSurvive() =>
        PropertyRunner.FromProcess().Check(Of(Slang, Misspelling, Repetition, Abbreviation, ExpressivePunctuation), sample => Assert.Empty(Lost(EveryStep(), sample)));

    [Fact]
    [Trait("Kind", "Property")]
    public void EmojiSymbolAndLanguageMixSurvive() =>
        PropertyRunner.FromProcess().Check(Of(Emoji, Symbol, LanguageMix), sample => Assert.Empty(Lost(EveryStep(), sample)));

    [Fact]
    [Trait("Kind", "Property")]
    public void CaseAndIrregularSpacingDoNotChange() =>
        PropertyRunner.FromProcess().Check(Of(Capitals, MixedCase), sample =>
        {
            Assert.Contains(sample.Marks, mark => mark.Kind == Spacing);
            Assert.Empty(Lost(EveryStep(), sample));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void EverythingOutsideDeclaredEditsReappearsInOrder() =>
        PropertyRunner.FromProcess().Check(AroundARemoval, generated =>
        {
            var (sample, removed) = generated;
            var length = Boundaries.CodePointCount(sample.Text);
            var outside = Enumerable.Range(0, length).Where(at => at < removed.Occurrence.Start || at >= removed.Occurrence.End).ToList();

            var sanitized = Sanitized(EveryStep(), sample.Text, removed);

            var moved = outside.Select(at => sanitized.Trail.Forward(at, at + 1)).ToList();
            Assert.All(moved, span => Assert.True(span.Exists));
            Assert.Equal(outside.Select(at => Cut(sample.Text, at, at + 1)), moved.Select(span => Cut(sanitized.Text, span.Start, span.End)));
            Assert.Equal(moved.Select(span => span.Start).Order(), moved.Select(span => span.Start));
            Assert.DoesNotContain(Identifier, sanitized.Text, StringComparison.Ordinal);
        });

    [Fact]
    public void StepThatFlattensAMarkIsRejectedEvenIfDeclared()
    {
        var flattening = new FixedStep("aspas", 1, new StepScope("troca as aspas tipográficas por aspas retas", "o resto do texto"), input =>
        [
            .. input.Text.EnumerateRunes().Select((rune, at) => (rune, at))
                .Where(point => point.rune.Value is 0x201C or 0x201D)
                .Select(point => SanitizationEdit.Rewriting(point.at, point.at + 1, "\"", Boundaries)),
        ]);
        var quoted = new SignalMark(10, 17, "“assim”", TypographicQuotes);
        var sample = new SignalSample("ela disse “assim” e foi", [quoted]);

        var lost = Lost(EveryStep(flattening), sample);

        Assert.True(flattening.Scope!.IsDeclared);
        Assert.Equal([quoted], lost);
    }
}
