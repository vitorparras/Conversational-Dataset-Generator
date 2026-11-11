using Core.Persistence.Custody;
using Core.Persistence.Ingestion;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>The repeating of a write that met another writer (issue 75): only the collision is repeated, at most three times in all, and the last answer is the one that comes back.</summary>
public sealed class BatchRetryTests
{
    private static readonly BatchResult Written = new() { Outcome = BatchOutcome.Opened, BatchId = 1 };
    private static readonly BatchResult Collided = new() { Outcome = BatchOutcome.NotRecorded, Refusal = CustodyRefusals.Conflict() };
    private static readonly BatchResult Refused = new() { Outcome = BatchOutcome.NotRecorded, Refusal = CustodyRefusals.ActorUnknown() };

    [Fact]
    public async Task AWriteThatLandsAtOnceIsMadeOnce()
    {
        var made = 0;

        var result = await BatchRetry.RunAsync(() => Task.FromResult(Count(ref made, Written)), BatchRetry.Attempts, TestContext.Current.CancellationToken);

        Assert.Equal((Written, 1), (result, made));
    }

    [Fact]
    public async Task AWriteThatCollidesTwiceAndThenLandsIsMadeThreeTimes()
    {
        var made = 0;

        var result = await BatchRetry.RunAsync(() => Task.FromResult(Count(ref made, made < 2 ? Collided : Written)), BatchRetry.Attempts, TestContext.Current.CancellationToken);

        Assert.Equal((Written, 3), (result, made));
    }

    [Fact]
    public async Task AWriteThatAlwaysCollidesIsMadeThreeTimesAndTheLastCollisionComesBack()
    {
        var made = 0;

        var result = await BatchRetry.RunAsync(() => Task.FromResult(Count(ref made, Collided)), BatchRetry.Attempts, TestContext.Current.CancellationToken);

        Assert.Equal((BatchRetry.Attempts, true), (made, result.Collided));
    }

    [Fact]
    public async Task ARefusalThatIsNotACollisionIsNotRepeated()
    {
        var made = 0;

        var result = await BatchRetry.RunAsync(() => Task.FromResult(Count(ref made, Refused)), BatchRetry.Attempts, TestContext.Current.CancellationToken);

        Assert.Equal((1, false, CustodyOutcome.ActorUnknown), (made, result.Collided, result.Refusal?.Outcome));
    }

    [Fact]
    public async Task TheWaitBetweenTheAttemptsFollowsTheCancellationOfTheCallerAndNoAttemptIsMadeAfterIt()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var made = 0;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => BatchRetry.RunAsync(() => Task.FromResult(Count(ref made, Collided)), BatchRetry.Attempts, cancelled.Token));

        Assert.Equal(1, made);
    }

    [Fact]
    public async Task TheWaitBetweenTwoAttemptsIsNeverShorterThanTheShortestWait()
    {
        var made = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        _ = await BatchRetry.RunAsync(() => Task.FromResult(Count(ref made, made < 2 ? Collided : Written)), BatchRetry.Attempts, TestContext.Current.CancellationToken);
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds >= 15, $"Two waits of at least ten milliseconds took {watch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void OnlyTheNotRecordedForAConflictIsACollision() =>
        Assert.Equal((false, false, true), (Written.Collided, Refused.Collided, Collided.Collided));

    private static BatchResult Count(ref int made, BatchResult answer)
    {
        made++;
        return answer;
    }
}
