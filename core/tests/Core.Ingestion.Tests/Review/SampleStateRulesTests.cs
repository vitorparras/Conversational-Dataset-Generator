using Core.Ingestion.Contracts;

namespace Core.Ingestion.Tests.Review;

/// <summary>
/// What the state of a batch refuses of the review of its sample, by one decision with no read and no write that the database takes alike (#837); a state that cannot happen,
/// reviewed without a draw, is still answered by the order of the rules.
/// </summary>
public sealed class SampleStateRulesTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ABatchThatDoesNotExistIsNotFoundWhateverIsAsked(bool drawing) =>
        Assert.Equal(SampleRefusal.NotFound, SampleStateRules.Refusal(null, drawing));

    [Theory]
    [InlineData(false, false, false, SampleRefusal.NotOpen)]
    [InlineData(false, true, false, SampleRefusal.NotOpen)]
    [InlineData(false, true, true, SampleRefusal.NotOpen)]
    [InlineData(true, true, false, SampleRefusal.AlreadySampled)]
    [InlineData(true, true, true, SampleRefusal.AlreadySampled)]
    [InlineData(true, false, false, null)]
    public void DrawingIsRefusedForABatchThatIsNotOpenOrAlreadySampledAndAllowedOtherwise(bool open, bool sampled, bool reviewed, SampleRefusal? expected) =>
        Assert.Equal(expected, SampleStateRules.Refusal(new SampledBatch(open, sampled, reviewed), drawing: true));

    [Theory]
    [InlineData(true, false, false, SampleRefusal.NotSampled)]
    [InlineData(true, false, true, SampleRefusal.NotSampled)]
    [InlineData(true, true, true, SampleRefusal.AlreadyReviewed)]
    [InlineData(true, true, false, null)]
    [InlineData(false, false, false, SampleRefusal.NotOpen)]
    [InlineData(false, true, false, SampleRefusal.NotOpen)]
    [InlineData(false, true, true, SampleRefusal.AlreadyReviewed)]
    [InlineData(false, false, true, SampleRefusal.NotSampled)]
    public void AReviewIsRefusedByTheStateOfTheBatchAndAllowedOnlyWhileTheSampleWaits(bool open, bool sampled, bool reviewed, SampleRefusal? expected) =>
        Assert.Equal(expected, SampleStateRules.Refusal(new SampledBatch(open, sampled, reviewed), drawing: false));
}
