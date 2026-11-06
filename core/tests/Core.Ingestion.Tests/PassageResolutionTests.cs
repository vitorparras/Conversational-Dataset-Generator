using Core.Boundary.Text;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests;

/// <summary>
/// For text with emoji, combined characters and pairs, a passage resolves to the same text every time it is asked, or is refused every time, and what is resolved is never a piece cut out of a pair
/// nor the neighbouring passage (CA-4).
/// </summary>
public sealed class PassageResolutionTests
{
    private static readonly FormConference Conference = new(new TextBoundaries());

    private static readonly string[] Alphabet = ["a", "Z", " ", "é", "\U0001F642", "\U0001F469‍\U0001F4BB", "\U0001D400", "é", "7"];

    private static readonly Gen<string> Pieces = Gen.Int[0, Alphabet.Length - 1].Select(at => Alphabet[at]);

    private static readonly Gen<string> Texts = Pieces.List[1, 8].Select(pieces => string.Join(string.Empty, pieces));

    private static readonly Gen<(string Text, Passage Passage)> Cases = Texts.SelectMany(text =>
        Gen.Select(Gen.Int[-1, text.Length + 1], Gen.Int[-1, text.Length + 1], (start, end) => (text, new Passage(start, end))));

    [Fact]
    [Trait("Kind", "Property")]
    public void APassageResolvesToTheSameTextTwiceOrIsRefusedTwiceAndNeverToAPieceOfAPair() =>
        PropertyRunner.FromProcess().Check(Cases, generated =>
        {
            var (text, passage) = generated;
            var read = Units.Read(Units.Labeled(text, passage));

            var first = Conference.Confer(MaterialForm.LabeledSequence, read);
            var second = Conference.Confer(MaterialForm.LabeledSequence, read);

            Assert.Equal(first.Refusal?.Reason, second.Refusal?.Reason);
            Assert.Equal(MustBeRefused(text, passage), first.Accepted is null);
            if (first.Accepted is LabeledSequence sequence)
            {
                var resolved = sequence.Texts[0].Resolve(0);
                Assert.Equal(resolved, ((LabeledSequence)second.Accepted!).Texts[0].Resolve(0));
                Assert.Equal(text[passage.Start..passage.End], resolved);
                Assert.False(char.IsLowSurrogate(resolved[0]) && !IsPairedBefore(text, passage.Start));
                Assert.False(char.IsHighSurrogate(resolved[^1]) && !IsPairedAfter(text, passage.End));
            }
        });

    /// <summary>The oracle, written from the rules of the form and not from the code: a blank text, a passage outside the text or with no length, or a limit between the halves of a pair.</summary>
    private static bool MustBeRefused(string text, Passage passage)
    {
        var outside = passage.Start < 0 || passage.End <= passage.Start || passage.End > text.Length;
        return string.IsNullOrWhiteSpace(text) || outside || Splits(text, passage);
    }

    private static bool Splits(string text, Passage passage) =>
        (passage.Start > 0 && char.IsSurrogatePair(text, passage.Start - 1)) || (passage.End > 0 && char.IsSurrogatePair(text, passage.End - 1));

    private static bool IsPairedBefore(string text, int start) => start > 0 && char.IsHighSurrogate(text[start - 1]);

    private static bool IsPairedAfter(string text, int end) => end < text.Length && char.IsLowSurrogate(text[end]);
}
