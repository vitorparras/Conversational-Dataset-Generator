using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests.Deciding;

/// <summary>The destination holds for every occurrence the service could send, and not for the ones someone thought of (issue 84, CA-2 and CA-3).</summary>
public sealed class DestinationPolicyPropertiesTests
{
    private static readonly Gen<string> Secret = Gen.OneOf(
        "CREDENTIAL",
        "SECRET",
        "PASSWORD",
        "TOKEN",
        "API_KEY",
        "PRIVATE_KEY");

    private static readonly Gen<string> Name = Gen.OneOf(OccurrenceKinds.Person, OccurrenceKinds.Organization, OccurrenceKinds.Location);

    private static readonly Gen<string> AnyText = Gen.String[Gen.Char['A', 'z'], 0, 8];

    private static readonly Gen<string> AnyRole = Gen.OneOf("nsubj", "nsubj:pass", "obj", "iobj", "vocative", "csubj", "obl", "obl:arg", "nmod", "advmod", "appos", "root", "flat", string.Empty);

    [Fact]
    [Trait("Kind", "Property")]
    public void ASecretIsRemovedWhateverThePartOfSpeechAndTheRole() =>
        PropertyRunner.FromProcess().Check(Gen.Select(Secret, AnyText, AnyText), generated =>
        {
            var (kind, partOfSpeech, role) = generated;

            Assert.Equal(Destination.Remove, DestinationPolicy.Of(Found.Occurrence(kind, partOfSpeech, role)));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void TheSameProperNameAsAPersonAndAsAPlaceNeverGetsTheSameDestinationWhateverTheFunction() =>
        PropertyRunner.FromProcess().Check(Gen.Select(AnyRole, Gen.Int[0, 50], Gen.Int[1, 20]), generated =>
        {
            var (role, start, length) = generated;

            var person = DestinationPolicy.Of(Found.Occurrence(OccurrenceKinds.Person, "PROPN", role, start, start + length));
            var place = DestinationPolicy.Of(Found.Occurrence(OccurrenceKinds.Location, "PROPN", role, start, start + length));

            Assert.NotEqual(person, place);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void TheGrammarBreaksTheTieOfAKindThatNamesAProperNameAndAWordThatIsNone() =>
        PropertyRunner.FromProcess().Check(Gen.Select(Name, AnyRole), generated =>
        {
            var (kind, role) = generated;

            var name = DestinationPolicy.Of(Found.Occurrence(kind, "PROPN", role));
            var common = DestinationPolicy.Of(Found.Occurrence(kind, "NOUN", role));

            Assert.Equal((Destination.Remove, true), (common, name != Destination.Remove));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void TheOffsetsAndTheFunctionNeverChangeTheDestination() =>
        PropertyRunner.FromProcess().Check(Gen.Select(Name, AnyText, AnyRole, AnyRole, Gen.Int[0, 100]), generated =>
        {
            var (kind, partOfSpeech, role, other, end) = generated;

            var here = DestinationPolicy.Of(Found.Occurrence(kind, partOfSpeech, role, 0, 1));
            var there = DestinationPolicy.Of(Found.Occurrence(kind, partOfSpeech, other, end, end + 1));

            Assert.Equal(here, there);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void EveryOccurrenceGetsOneOfTheThreeDestinations() =>
        PropertyRunner.FromProcess().Check(Gen.Select(AnyText, AnyText, AnyText), generated =>
        {
            var (kind, partOfSpeech, role) = generated;

            Assert.True(Enum.IsDefined(DestinationPolicy.Of(Found.Occurrence(kind, partOfSpeech, role))));
        });
}
