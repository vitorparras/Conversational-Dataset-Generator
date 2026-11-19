using Core.Ingestion.Deciding;
using Core.Ingestion.Sanitizing;
using Core.Tests.Support.Harness;
using CsCheck;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>The same material and the same version of the steps give the same sanitized text, in this process or any other (issue 87, CA-4).</summary>
public sealed class SanitizationDeterminismTests
{
    private const string DestinationsOnly = "c3625e9d4b7d4cb5c5a90960ac5f08a23b74c0d7915975fdcd329095d04911ec";

    private static readonly Gen<string> Pieces = Gen.OneOf("Qz", "\u00E9", "e\u0301", "\U0001F600", "\U0001F468\u200D\U0001F469", " ", "7");

    private static readonly Gen<(string Raw, DecidedOccurrence[] Decided)> Materials = Gen.Int[1, 10].SelectMany(count => Pieces.Array[count]).SelectMany(pieces =>
    {
        var raw = string.Concat(pieces);
        var length = raw.EnumerateRunes().Count();
        var occurrence = Gen.Int[0, length].SelectMany(start => Gen.Int[start, length].SelectMany(end => Gen.Int[0, 3].Select(kind => Of(kind, start, end))));
        return occurrence.Array[0, 4].Select(decided => (raw, decided));
    });

    [Fact]
    [Trait("Kind", "Property")]
    public void SameMaterialAndVersionGiveTheSameTextInAnyProcess() =>
        PropertyRunner.FromProcess().Check(Materials, generated =>
        {
            var (raw, decided) = generated;

            var here = Sanitized(OfDestinations(), raw, decided);
            var there = Sanitized(OfDestinations(), raw, decided);

            Assert.Equal((here.Version, here.Text), (there.Version, there.Text));
        });

    [Fact]
    public void TheVersionIsDerivedFromTheDeclaredStepsAloneAndNotFromTheProcess() =>
        Assert.Equal(DestinationsOnly, new SanitizationSteps([new DestinationsStep()]).Version);

    private static DecidedOccurrence Of(int kind, int start, int end) => kind switch
    {
        0 => Credential(start, end),
        1 => Data(start, end),
        2 => Pseudonymized(start, end),
        _ => Preserved(start, end),
    };
}
