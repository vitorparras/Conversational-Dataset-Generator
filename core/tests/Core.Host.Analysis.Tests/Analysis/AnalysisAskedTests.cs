using Core.Host.Analysis;

namespace Core.Host.Tests.Analysis;

/// <summary>What is asked of the analysis names its language and the reference it resumes, and never its text (requirement 021).</summary>
public sealed class AnalysisAskedTests
{
    [Fact]
    public void DescriptionNamesTheLanguageAndTheReferenceResumedAndNeverTheText()
    {
        var resumed = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        var asked = new AnalysisAsked("falei com PESSOA_1 ontem ZQX-MARKER-0027-ASKED", "pt", resumed);

        var description = asked.ToString();

        Assert.Equal($"AnalysisAsked {{ Language = pt, Resumes = {resumed} }}", description);
        Assert.DoesNotContain("ZQX-MARKER-0027-ASKED", description, StringComparison.Ordinal);
    }
}
