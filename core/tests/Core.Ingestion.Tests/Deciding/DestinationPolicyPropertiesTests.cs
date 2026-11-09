using Core.Ingestion.Deciding;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests.Deciding;

/// <summary>The destination holds for every occurrence the service could send, and not for the ones someone thought of (issue 84, CA-2 and CA-3).</summary>
public sealed class DestinationPolicyPropertiesTests
{
    private static readonly Gen<string> Secret = Gen.OneOf("CREDENTIAL", "SECRET", "PASSWORD", "TOKEN", "API_KEY", "PRIVATE_KEY", "credential", "Secret");

    private static readonly Gen<string> AnyText = Gen.String[Gen.Char['A', 'z'], 0, 8];

    private static readonly Gen<string> OutsideRoles = Gen.OneOf("obl", "obl:arg", "nmod", "advmod", "appos", "root", "flat", string.Empty);

    private static readonly Gen<string> TakingPart = Gen.OneOf("nsubj", "nsubj:pass", "obj", "iobj", "vocative", "csubj");

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
    public void TheSameProperNameAsAPersonAndAsAPlaceNeverGetsTheSameDestination() =>
        PropertyRunner.FromProcess().Check(Gen.Select(OutsideRoles, Gen.Int[0, 50], Gen.Int[1, 20]), generated =>
        {
            var (role, start, length) = generated;

            var person = DestinationPolicy.Of(Found.Occurrence("PERSON", "PROPN", role, start, start + length));
            var place = DestinationPolicy.Of(Found.Occurrence("LOCATION", "PROPN", role, start, start + length));

            Assert.NotEqual(person, place);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void ThePlaceIsPreservedOutsideTheRolesOfWhoTakesPartAndPseudonymizedInThem() =>
        PropertyRunner.FromProcess().Check(Gen.Select(OutsideRoles, TakingPart), generated =>
        {
            var (outside, taking) = generated;

            var aside = DestinationPolicy.Of(Found.Occurrence("LOCATION", "PROPN", outside));
            var acting = DestinationPolicy.Of(Found.Occurrence("LOCATION", "PROPN", taking));

            Assert.Equal((Destination.Preserve, Destination.Pseudonymize), (aside, acting));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void TheOffsetsNeverChangeTheDestination() =>
        PropertyRunner.FromProcess().Check(Gen.Select(AnyText, AnyText, AnyText, Gen.Int[0, 100]), generated =>
        {
            var (kind, partOfSpeech, role, end) = generated;

            var here = DestinationPolicy.Of(Found.Occurrence(kind, partOfSpeech, role, 0, 1));
            var there = DestinationPolicy.Of(Found.Occurrence(kind, partOfSpeech, role, end, end + 1));

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
