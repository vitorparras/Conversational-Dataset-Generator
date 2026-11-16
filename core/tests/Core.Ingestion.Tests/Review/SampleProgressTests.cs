using Core.Ingestion.Review;

namespace Core.Ingestion.Tests.Review;

/// <summary>What the review of a drawn sample allows at each point of it, with nothing written (#610 CA-1, CA-4).</summary>
public sealed class SampleProgressTests
{
    [Theory]
    [InlineData(5, 0, 1)]
    [InlineData(5, 2, 3)]
    [InlineData(5, 4, 5)]
    public void NextItemIsTheOneAfterTheLastReviewed(int size, int reviewed, int expected)
    {
        var next = new SampleProgress(size, reviewed).Next;

        Assert.Equal(expected, next);
    }

    [Theory]
    [InlineData(5, 2, 4)]
    [InlineData(5, 0, 2)]
    [InlineData(5, 2, 2)]
    public void ItemAfterTheNextIsRefused(int size, int reviewed, int position)
    {
        var decision = new SampleProgress(size, reviewed).Mark(position);

        Assert.Equal(SampleDecision.ItemIsNotNext, decision);
    }

    [Theory]
    [InlineData(5, 4)]
    [InlineData(1, 0)]
    [InlineData(5, 0)]
    public void ApprovalBeforeTheLastItemIsRefused(int size, int reviewed)
    {
        var decision = new SampleProgress(size, reviewed).Approve();

        Assert.Equal(SampleDecision.ReviewIncomplete, decision);
    }

    [Theory]
    [InlineData(5, 2, 4)]
    [InlineData(5, 0, 3)]
    public void FindingOnAnItemNotReachedIsRefused(int size, int reviewed, int position)
    {
        var decision = new SampleProgress(size, reviewed).Find(position);

        Assert.Equal(SampleDecision.ItemIsNotNext, decision);
    }

    [Theory]
    [InlineData(5, 2, 1)]
    [InlineData(5, 2, 3)]
    [InlineData(5, 5, 5)]
    [InlineData(5, 0, 1)]
    public void FindingOnAnItemReviewedOrBeingReviewedIsAllowed(int size, int reviewed, int position)
    {
        var decision = new SampleProgress(size, reviewed).Find(position);

        Assert.Equal(SampleDecision.Allowed, decision);
    }

    [Fact]
    public void ApprovalAfterTheLastItemIsAllowed()
    {
        var decision = new SampleProgress(3, 3).Approve();

        Assert.Equal(SampleDecision.Allowed, decision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void APlaceOutsideTheSampleIsRefused(int position)
    {
        var decision = new SampleProgress(5, 2).Mark(position);

        Assert.Equal(SampleDecision.ItemOutsideTheSample, decision);
    }
}
