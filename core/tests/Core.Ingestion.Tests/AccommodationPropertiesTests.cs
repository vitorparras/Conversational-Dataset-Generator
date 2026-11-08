using Core.Boundary.Text;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests;

/// <summary>
/// Accommodating is the exception and never the default (issue 81, CA-4): for any material that fits exactly into a form, built unit by unit with what that form keeps and no more,
/// the conference of the form accepts it and no unit declares a loss. And what is declared when a material brings more than the form keeps is always a closed set of codes, at least one,
/// with the text taken from the code and never from the material.
/// </summary>
public sealed class AccommodationPropertiesTests
{
    private static readonly FormConference Conference = new(new TextBoundaries());

    private static readonly DateTimeOffset Moment = new(2031, 3, 4, 8, 0, 0, TimeSpan.Zero);

    private static readonly Gen<string> Marked = Gen.String[Gen.Char['A', 'Z'], 1, 8].Select(letters => "ZQX" + letters);

    private static readonly Gen<(MaterialForm Form, ReadMaterial Material)> Exact = Gen.Int[0, 3].SelectMany(form => Gen.Int[1, 5].SelectMany(count => Gen.Select(
        Marked.Array[count],
        Marked.Array[count],
        Gen.Bool.Array[count],
        (texts, authors, replies) => (Form: (MaterialForm)form, Material: FitsExactly((MaterialForm)form, texts, authors, replies)))));

    private static readonly Gen<(MaterialForm Form, ReadMaterial Material)> Anything = Gen.Int[0, 3].SelectMany(form => Gen.Int[0, 5].SelectMany(count => Gen.Select(
        Marked.Array[count],
        Marked.Array[count],
        Gen.Bool.Array[count],
        (texts, authors, replies) => (Form: (MaterialForm)form, Material: Carrying(texts, authors, replies)))));

    [Fact]
    [Trait("Kind", "Property")]
    public void MaterialThatFitsAFormExactlyDeclaresNoLossAndTheConferenceAcceptsIt() =>
        PropertyRunner.FromProcess().Check(Exact, generated =>
        {
            var (form, material) = generated;

            var declared = Accommodating.Of(material, form);

            Assert.NotNull(Conference.Confer(form, ReadOutcome.Read(material)).Accepted);
            Assert.Equal(material.Units.Count, declared.Count);
            Assert.All(declared, accommodation => Assert.Null(accommodation));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void WhatIsDeclaredIsAlwaysAtLeastOneCodeOfTheSetWithTheTextOfTheCodeAndNeverOfTheMaterial() =>
        PropertyRunner.FromProcess().Check(Anything, generated =>
        {
            var (form, material) = generated;

            var declared = Accommodating.Of(material, form).Where(accommodation => accommodation is not null).Select(accommodation => accommodation!).ToList();

            Assert.All(declared, accommodation =>
            {
                Assert.Equal(form, accommodation.Received);
                Assert.NotEmpty(accommodation.Losses);
                Assert.Equal(accommodation.Losses.Distinct().Order(), accommodation.Losses);
                Assert.All(accommodation.Codes, code => Assert.NotNull(LossCodes.Of(code)));
                Assert.All(accommodation.Words, words => Assert.DoesNotContain("ZQX", words, StringComparison.Ordinal));
            });
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void ALossIsDeclaredExactlyWhenTheUnitCarriesWhatTheFormDoesNotKeep() =>
        PropertyRunner.FromProcess().Check(Anything, generated =>
        {
            var (form, material) = generated;

            var declared = Accommodating.Of(material, form);

            Assert.Equal(
                material.Units.Select(unit => Carries(unit, form, material.Units.Count)),
                declared.Select(accommodation => accommodation is not null));
        });

    private static bool Carries(ReadUnit unit, MaterialForm form, int units) =>
        (unit.Author is not null && form is MaterialForm.Document or MaterialForm.LabeledSequence)
        || (unit.Instant is not null && form is MaterialForm.Document or MaterialForm.LabeledSequence)
        || (unit.RepliesTo is not null && form != MaterialForm.Conversation)
        || (unit.Passages.Count > 0 && form != MaterialForm.LabeledSequence)
        || (form == MaterialForm.Comment && units > 1);

    /// <summary>What the form keeps of each unit and not a word more: the conversation its author, its instants (one after the other) and answers to a unit before; the comment its author;
    /// the document its text; the labeled sequence its text and a passage that is the whole of it.</summary>
    private static ReadMaterial FitsExactly(MaterialForm form, string[] texts, string[] authors, bool[] replies) => form switch
    {
        MaterialForm.Conversation => new ReadMaterial(
            texts.Select((text, at) => new ReadUnit(text, authors[at], Moment.AddSeconds(at), replies[at] && at > 0 ? at - 1 : null, new List<Passage>())).ToList()),
        MaterialForm.Comment => new ReadMaterial(new List<ReadUnit> { new(texts[0], authors[0], replies[0] ? Moment : null, null, new List<Passage>()) }),
        MaterialForm.Document => new ReadMaterial(texts.Select(text => new ReadUnit(text, null, null, null, new List<Passage>())).ToList()),
        _ => new ReadMaterial(texts.Select(text => new ReadUnit(text, null, null, null, new List<Passage> { new(0, text.Length) })).ToList()),
    };

    /// <summary>Units that carry now one thing, now another, whatever the form: an author, an instant, an answer, a passage.</summary>
    private static ReadMaterial Carrying(string[] texts, string[] authors, bool[] flags) =>
        new(texts.Select((text, at) => new ReadUnit(
            text,
            flags[at] ? authors[at] : null,
            at % 2 == 0 ? Moment.AddSeconds(at) : null,
            at % 3 == 1 ? at - 1 : null,
            at % 4 == 2 ? new List<Passage> { new(0, text.Length) } : new List<Passage>())).ToList());
}
