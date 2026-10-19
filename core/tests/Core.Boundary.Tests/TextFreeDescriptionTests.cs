using System.Reflection;
using Core.Boundary.Contracts;
using Core.Platform.Logging;

namespace Core.Boundary.Tests;

/// <summary>A message described by accident, in a log line or an exception, shows neither the text nor an offset.</summary>
public sealed class TextFreeDescriptionTests
{
    private const string Marker = "ZQX-MARKER-5519";
    private static readonly Occurrence Found = new(48213, 48271, "PERSON", "PROPN", "obl");

    [Fact]
    public void RequestDescriptionLeavesOutTheText()
    {
        var request = new AnalysisRequest($"\U0001F642 falei com {Marker}", "pt", "ZQX-REF-0003", ContractVersion.Current);

        var description = request.ToString();

        Assert.DoesNotContain(Marker, description, StringComparison.Ordinal);
        Assert.Contains("TextChars = 28", description, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestTextIsClassifiedAsMaterial()
    {
        var text = typeof(AnalysisRequest).GetProperty(nameof(AnalysisRequest.Text));

        Assert.NotNull(text?.GetCustomAttribute<MaterialAttribute>());
    }

    [Fact]
    public void OccurrenceDescriptionLeavesOutTheOffsets()
    {
        var description = Found.ToString();

        Assert.DoesNotContain("48213", description, StringComparison.Ordinal);
        Assert.DoesNotContain("48271", description, StringComparison.Ordinal);
        Assert.Contains("SyntacticRole = obl", description, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponseDescriptionCountsOccurrencesWithoutTheirOffsets()
    {
        var response = new AnalysisResponse([Found], "zqx-model-1");

        var description = response.ToString();

        Assert.DoesNotContain("48213", description, StringComparison.Ordinal);
        Assert.DoesNotContain("48271", description, StringComparison.Ordinal);
        Assert.Contains("Occurrences = 1", description, StringComparison.Ordinal);
    }

    [Fact]
    public void OutcomeDescriptionLeavesOutTheOffsets()
    {
        var outcome = RecognitionOutcome.Analyzed(new AnalysisResponse([Found], "zqx-model-1"));

        var description = outcome.ToString();

        Assert.DoesNotContain("48213", description, StringComparison.Ordinal);
        Assert.DoesNotContain("48271", description, StringComparison.Ordinal);
    }
}
