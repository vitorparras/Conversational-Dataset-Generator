using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests;

/// <summary>
/// Whatever the reader hands over and whatever the form declared, the conference gives one of the two outcomes, an accepted item has the declared form,
/// and no reason names a word of a unit.
/// </summary>
public sealed class ConferenceInvariantsTests
{
    private static readonly FormConference Conference = FormConference.Minimal();

    /// <summary>Texts of capital letters only, which no word of the reasons (all in lower case, but for the names of the fields) can spell.</summary>
    private static readonly Gen<string> Marked = Gen.String[Gen.Char['A', 'Z'], 8, 16];

    private static readonly Gen<Passage> Passages = Gen.Select(Gen.Int[-1, 20], Gen.Int[-1, 20], (start, end) => new Passage(start, end));

    private static readonly Gen<ReadUnit> UnitGen = Gen.Select(
        Marked,
        Marked.Null(),
        Gen.Int[0, 3].Select(second => new DateTimeOffset(2031, 1, 1, 0, 0, second, TimeSpan.Zero)).Nullable(),
        Gen.Int[-1, 6].Nullable(),
        Passages.List[0, 2],
        (text, author, instant, repliesTo, passages) => new ReadUnit(text, author, instant, repliesTo, passages));

    private static readonly Gen<ReadOutcome> Outcomes = Gen.Frequency(
        (1, Gen.Enum<UnreadableReason>().Select(ReadOutcome.Unreadable)),
        (9, UnitGen.List[0, 5].Select(units => ReadOutcome.Read(new ReadMaterial(units)))));

    [Fact]
    [Trait("Kind", "Property")]
    public void TheConferenceAlwaysGivesOneOfTheTwoOutcomesAndTheAcceptedItemHasTheDeclaredForm() =>
        PropertyRunner.FromProcess().Check(Gen.Select(Gen.Enum<MaterialForm>(), Outcomes), generated =>
        {
            var (form, outcome) = generated;

            var result = Conference.Confer(form, outcome);

            Assert.True((result.Accepted is null) != (result.Refusal is null));
            Assert.True(result.Accepted is null || result.Accepted.Form == form);
            Assert.True(result.Refusal is null || result.Refusal.Form == form);
            Assert.True(outcome.Material is not null || result.Accepted is null);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void NoReasonOfRefusalHoldsATextAnAuthorOrAnAnsweredAuthorOfAUnit() =>
        PropertyRunner.FromProcess().Check(Gen.Select(Gen.Enum<MaterialForm>(), Outcomes), generated =>
        {
            var (form, outcome) = generated;

            var refusal = Conference.Confer(form, outcome).Refusal;

            if (refusal is not null)
            {
                var words = outcome.Material?.Units.SelectMany(unit => new[] { unit.Text, unit.Author }).OfType<string>() ?? [];
                Assert.All(words, word => Assert.DoesNotContain(word, refusal.Reason, StringComparison.Ordinal));
                Assert.False(string.IsNullOrWhiteSpace(refusal.Reason));
            }
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void AConversationAcceptedIsInTheOrderOfTheMomentsWhateverTheOrderOfTheUnits() =>
        PropertyRunner.FromProcess().Check(UnitGen.List[1, 5], units =>
        {
            var result = Conference.Confer(MaterialForm.Conversation, ReadOutcome.Read(new ReadMaterial(units)));

            if (result.Accepted is Conversation conversation)
            {
                Assert.Equal(conversation.Messages.Select(message => message.Instant).Order().ToArray(), conversation.Messages.Select(message => message.Instant).ToArray());
            }
        });
}
