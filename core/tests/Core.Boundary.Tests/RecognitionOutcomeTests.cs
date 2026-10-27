using Core.Boundary.Contracts;

namespace Core.Boundary.Tests;

/// <summary>An outcome is an analysis or an unavailability, and nothing else can be built.</summary>
public sealed class RecognitionOutcomeTests
{
    private static readonly AnalysisResponse NothingFound = new([], null);

    [Fact]
    public void AnalysisWithNothingFoundIsNotUnavailability()
    {
        var outcome = RecognitionOutcome.Analyzed(NothingFound);

        Assert.Same(NothingFound, outcome.Response);
        Assert.Null(outcome.Reason);
    }

    [Theory]
    [InlineData(UnavailabilityReason.TimedOut)]
    [InlineData(UnavailabilityReason.ConnectionRefused)]
    [InlineData(UnavailabilityReason.MalformedResponse)]
    [InlineData(UnavailabilityReason.UnknownContractVersion)]
    public void UnavailabilityCarriesItsExactReasonAndNoAnalysis(UnavailabilityReason reason)
    {
        var outcome = RecognitionOutcome.Unavailable(reason);

        Assert.Equal(reason, outcome.Reason);
        Assert.Null(outcome.Response);
    }

    [Fact]
    public void ReasonOutsideTheEnumerationIsADefect()
    {
        var unknown = (UnavailabilityReason)99;

        var defect = Assert.Throws<ArgumentOutOfRangeException>(() => RecognitionOutcome.Unavailable(unknown));

        Assert.Equal("reason", defect.ParamName);
    }

    [Fact]
    public void ReasonOutsideTheEnumerationIsRefusedSayingSoAndShowingTheValue()
    {
        var unknown = (UnavailabilityReason)99;

        var defect = Assert.Throws<ArgumentOutOfRangeException>(() => RecognitionOutcome.Unavailable(unknown));

        Assert.Contains("The reason is not one of the enumeration.", defect.Message, StringComparison.Ordinal);
        Assert.Equal(unknown, defect.ActualValue);
    }

    [Fact]
    public void AnalysisWithoutAResponseIsADefect()
    {
        AnalysisResponse? missing = null;

        var defect = Assert.Throws<ArgumentNullException>(() => RecognitionOutcome.Analyzed(missing!));

        Assert.Equal("response", defect.ParamName);
    }

    [Fact]
    public void NoThirdCaseCanBeBuiltOrDerived()
    {
        var type = typeof(RecognitionOutcome);

        Assert.True(type.IsSealed);
        Assert.Empty(type.GetConstructors());
    }
}
