using Core.Ingestion.Sanitizing;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>Every step reports what it changed, a step that changed nothing included, and the report carries no character of the text (issue 93).</summary>
public sealed class SanitizationReportTests
{
    [Fact]
    public void ReportCountsEveryStepIncludingThoseWithZero()
    {
        const string raw = "ab QXZ7 cd KWP9 ef";

        var sanitized = Sanitized(Sanitizer(FixedStep.Nothing("vazio"), new DestinationsStep()), raw, Credential(3, 7), Data(11, 15));

        Assert.Equal([("vazio", 1, 0), ("destinos", 1, 2)], sanitized.Report.Select(step => (step.Name, step.Version, step.Count)));
        Assert.Equal(
            [new SanitizationEdit(3, 7, SanitizationMarker.Credential), new SanitizationEdit(11, 15, SanitizationMarker.Data)],
            sanitized.Report[1].Edits);
    }

    [Fact]
    public void ReportShowsNoCharacterOfTheText()
    {
        const string raw = "ab QXZ7 cd KWP9 ef";

        var sanitized = Sanitized(raw, Credential(3, 7), Data(11, 15));

        var shown = string.Join(" ", sanitized.Report.Select(step => step.ToString()).Append(sanitized.ToString()));
        Assert.DoesNotContain("QXZ7", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("KWP9", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("ab ", shown, StringComparison.Ordinal);
        Assert.Contains("destinos", shown, StringComparison.Ordinal);
    }
}
