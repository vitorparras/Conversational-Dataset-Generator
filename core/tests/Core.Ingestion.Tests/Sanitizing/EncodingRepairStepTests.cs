using Core.Ingestion.Sanitizing;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>The repair puts back what a wrong encoding misread and nothing else; where two readings stand and none dominates, it leaves the text and counts it (issue 88).</summary>
public sealed class EncodingRepairStepTests
{
    private static SanitizedText Repaired(string raw) => Sanitized(Sanitizer(new EncodingRepairStep(Boundaries)), raw);

    [Fact]
    public void SwappedAccentsReturnToTheirSpellingAndNothingElseChanges()
    {
        const string raw = "Ele n\u00C3\u00A3o foi \u00C3\u00A0 reuni\u00C3\u00A3o, voc\u00C3\u00AA viu?"
            + " A\u00C3\u2021\u00C3\u0192O \u00E2\u20AC\u0153zqx\u00E2\u20AC\u009D \u00F0\u0178\u02DC\u20AC  fim"
            + " 30\u00C2\u00B0C \u00E2\u201D\u20AC \u00C2\u00BFqu\u00E9? \u00C3\u0080 zqx \u00E2\u2013\u00B6";

        var repaired = Repaired(raw);

        Assert.Equal("Ele n\u00E3o foi \u00E0 reuni\u00E3o, voc\u00EA viu? A\u00C7\u00C3O \u201Czqx\u201D \U0001F600  fim 30\u00B0C \u2500 \u00BFqu\u00E9? \u00C0 zqx \u25B6", repaired.Text);
        Assert.Equal(("codificacao", 14, 0), (repaired.Report[0].Name, repaired.Report[0].Count, repaired.Report[0].Untreated));
    }

    [Fact]
    public void TextWithoutCorruptionLeavesIdentical()
    {
        const string raw = "Bom dia, tudo bem? At\u00E9 amanh\u00E3 \u00E0s 10h, na sala \u00C1GUA 3. N\u00C3O \u00C9 \u00D3BVIO. \u00C3\u0001 \u00C3\uD800x";

        var repaired = Repaired(raw);

        Assert.Equal((raw, 0, 0), (repaired.Text, repaired.Report[0].Count, repaired.Report[0].Untreated));
    }

    [Fact]
    public void EmojiCaseRepetitionQuotesAndLanguageMixSurviveTheRepair()
    {
        const string raw = "\u201CEEEEEita\u201D N\u00C3O!!! \U0001F602\U0001F602 so cute\u2026 \u2018t\u00E1\u2019 \uFF21\uFF22 e\u0301 a\u200Bb \uFB01m \u2014 vamo?!"
            + " gritou \u00ABAMANH\u00C3\u00BB e IRM\u00C3\u00A0 noite, mAm\u00C3\u00BB";

        var repaired = Repaired(raw);

        Assert.Equal((raw, 0), (repaired.Text, repaired.Report[0].Count));
    }

    [Fact]
    public void RepairTwiceEqualsRepairOnce()
    {
        const string raw = "caf\u00C3\u0192\u00C2\u00A9 e p\u00C3\u00A3o, AMANH\u00C3\u2026 \u00C3\uFFFDGUA";

        var once = Repaired(raw).Text;
        var twice = Repaired(once).Text;

        Assert.Equal("caf\u00E9 e p\u00E3o, AMANH\u00C3\u2026 \u00C3\uFFFDGUA", once);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void AmbiguousRepairIsLeftAsWrittenAndCounted()
    {
        const string raw = "\u00C3\uFFFDGUA fria, IRM\u00C3\u201D e AMANH\u00C3\u2026 mas n\u00C3\u00A3o \u00ABAMANH\u00C3\u00BB e IRM\u00C3\u00A0 e \U0001D408\U0001D411\U0001D40C\u00C3\u00A0 fim";

        var repaired = Repaired(raw);

        Assert.Equal(
            "\u00C3\uFFFDGUA fria, IRM\u00C3\u201D e AMANH\u00C3\u2026 mas n\u00E3o \u00ABAMANH\u00C3\u00BB e IRM\u00C3\u00A0 e \U0001D408\U0001D411\U0001D40C\u00C3\u00A0 fim",
            repaired.Text);
        Assert.Equal((1, 6), (repaired.Report[0].Count, repaired.Report[0].Untreated));
    }

    [Fact]
    public void NoBoundariesOrNoInputIsADefect()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new EncodingRepairStep(null!));
        _ = Assert.Throws<ArgumentNullException>(() => new EncodingRepairStep(Boundaries).Edits(null!));
        _ = Assert.Throws<ArgumentNullException>(() => new EncodingRepairStep(Boundaries).Untreated(null!));
    }
}
