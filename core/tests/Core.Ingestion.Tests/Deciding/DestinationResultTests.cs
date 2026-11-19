using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;

namespace Core.Ingestion.Tests.Deciding;

/// <summary>The result of the analysis is either the item decided or the reason no analysis came; never both, never neither (issue 84, CA-1).</summary>
public sealed class DestinationResultTests
{
    [Fact]
    public void ADecidedResultWithNoItemIsRefused()
    {
        var refused = Assert.Throws<ArgumentNullException>(() => DestinationResult.Of(null!));

        Assert.Equal("decided", refused.ParamName);
    }

    [Fact]
    public void AnUnavailableResultCarriesTheReasonAndNoItem()
    {
        var result = DestinationResult.Unavailable(UnavailabilityReason.MalformedResponse);

        Assert.Equal((null, UnavailabilityReason.MalformedResponse), (result.Decided, result.Reason));
    }
}
