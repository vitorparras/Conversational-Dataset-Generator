using Core.Ingestion.Contracts;
using Core.Ingestion.Review;

namespace Core.Ingestion.Tests.Review;

/// <summary>What the state of a batch refuses of the review of its sample, by one decision with no read and no write that the database takes alike (#837).</summary>
public sealed class SampleStateRulesTests
{
    private static readonly bool[] Both = [true, false];

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
    [InlineData(true, true, true, SampleRefusal.AlreadyReviewed)]
    [InlineData(true, true, false, null)]
    [InlineData(false, false, false, SampleRefusal.NotOpen)]
    [InlineData(false, true, false, SampleRefusal.NotOpen)]
    [InlineData(false, true, true, SampleRefusal.AlreadyReviewed)]
    public void AReviewIsRefusedByTheStateOfTheBatchAndAllowedOnlyWhileTheSampleWaits(bool open, bool sampled, bool reviewed, SampleRefusal? expected) =>
        Assert.Equal(expected, SampleStateRules.Refusal(new SampledBatch(open, sampled, reviewed), drawing: false));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheSampleProgressTakesTheSameDecisionAsTheRulesOverEveryState(bool drawing)
    {
        var states = from open in Both
                     from sampled in Both
                     from reviewed in Both
                     select (SampledBatch?)new SampledBatch(open, sampled, reviewed);

        Assert.All(states.Append(null), state => Assert.Equal(SampleStateRules.Refusal(state, drawing), SampleProgress.Refusal(state, drawing)));
    }
}
