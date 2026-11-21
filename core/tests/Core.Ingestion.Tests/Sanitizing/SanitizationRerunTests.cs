using Core.Ingestion.Deciding;
using Core.Ingestion.Sanitizing;
using Core.Tests.Support.Harness;
using CsCheck;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>
/// The rerun of the sanitization of an item (issue 97): the steps in force run again over the raw text with the destinations decided when it came in, so the same raw text and the same
/// version give the text held, said identical (CA-1); a text held that is not what the steps make of the raw text is not the starting point, but what the rerun replaces; and an item
/// whose destinations were never decided is refused, never sanitized as if nothing had to go.
/// </summary>
public sealed class SanitizationRerunTests
{
    private static readonly Gen<string> Pieces = Gen.OneOf("Qz", "\u00E9", "e\u0301", "\U0001F600", "\U0001F468\u200D\U0001F469", " ", "7", "Kq7Zx9", "\u00C3\u00A9", "**");

    private static readonly Gen<(string Raw, DecidedOccurrence[] Decided)> Materials = Gen.Int[1, 10].SelectMany(count => Pieces.Array[count]).SelectMany(pieces =>
    {
        var raw = string.Concat(pieces);
        var length = raw.EnumerateRunes().Count();
        var occurrence = Gen.Int[0, length].SelectMany(start => Gen.Int[start, length].SelectMany(end => Gen.Bool.Select(secret => secret ? Credential(start, end) : Pseudonymized(start, end))));
        return occurrence.Array[0, 4].Select(decided => (raw, decided));
    });

    [Fact]
    [Trait("Kind", "Property")]
    public void RerunOfTheSameVersionGivesTheIdenticalText() =>
        PropertyRunner.FromProcess().Check(Materials, generated =>
        {
            var (raw, decided) = generated;
            var held = Sanitized(InForce(), raw, decided);

            var rerun = new SanitizationRerun(InForce()).Rerun(raw, decided, held.Text);

            Assert.True(rerun.Identical);
            Assert.Equal((held.Version, held.Text), (rerun.Result.Sanitized?.Version, rerun.Result.Sanitized?.Text));
        });

    [Fact]
    public void RerunStartsFromTheRawTextAndNotFromTheTextHeld()
    {
        const string raw = "token Kq7Zx9 do Wvkt fim";
        var decided = new[] { Credential(6, 12), Pseudonymized(16, 20) };

        var rerun = new SanitizationRerun(InForce()).Rerun(raw, decided, "zqx texto guardado de outra lista");

        Assert.False(rerun.Identical);
        Assert.Equal(Sanitized(InForce(), raw, decided).Text, rerun.Result.Sanitized?.Text);
    }

    [Fact]
    public void ATextThatDiffersOnlyInCaseIsNotIdentical()
    {
        const string raw = "token Kq7Zx9 do Wvkt fim";
        var decided = new[] { Credential(6, 12) };
        var held = Sanitized(InForce(), raw, decided).Text.ToUpperInvariant();

        var rerun = new SanitizationRerun(InForce()).Rerun(raw, decided, held);

        Assert.NotEqual(held, rerun.Result.Sanitized?.Text);
        Assert.False(rerun.Identical);
    }

    [Fact]
    public void TheRerunShowsNoWordOfTheText()
    {
        const string raw = "token Kq7Zx9 do Wvkt fim";

        var shown = new SanitizationRerun(InForce()).Rerun(raw, [Credential(6, 12)], "zqx guardado").ToString();

        Assert.DoesNotContain("Wvkt", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("token", shown, StringComparison.Ordinal);
        Assert.Contains(InForce().Version, shown, StringComparison.Ordinal);
    }

    [Fact]
    public void RerunOfAnItemWithoutDecidedDestinationsIsRefused()
    {
        var rerun = new SanitizationRerun(InForce()).Rerun("token Kq7Zx9 fim", null, "zqx texto guardado");

        Assert.Equal((SanitizationRefusal.Undecided, false), (rerun.Result.Refusal, rerun.Identical));
        Assert.Null(rerun.Result.Sanitized);
    }

    [Fact]
    public void TheVersionOfTheRerunIsTheVersionOfTheStepsInForce() =>
        Assert.Equal(InForce().Version, new SanitizationRerun(InForce()).Version);

    /// <summary>The list of steps the host declares, in its order.</summary>
    private static Sanitizer InForce() =>
        Sanitizer(new EncodingRepairStep(Boundaries), new FormatArtifactStep(Boundaries), new IdentifierStep(Boundaries), new DestinationsStep());
}
