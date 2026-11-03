using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>
/// The service that verifies the chain without being asked (issue #61, part 2): one verification as soon as it starts and one every interval, never two at once; a broken chain and a
/// verification that does not end are told in the host's log and neither stops the service; stopping it is not a failure. The clock moves only when the test says so.
/// </summary>
public sealed class ChainVerificationServiceTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly VerificationSchedule Schedule = new(Interval);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task VerificationRunsAsSoonAsTheServiceStartsAndTheNextWaitsTheInterval()
    {
        await using var run = new Run(_ => Task.FromResult(Result(null, CustodyOutcome.Appended)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        Assert.Equal((1, 1, (TimeSpan?)Interval), (run.Calls, run.Clock.Waiting, run.Clock.NextDue));
    }

    [Fact]
    public async Task ClockAdvancedByTheIntervalRunsAnotherAndLessDoesNot()
    {
        await using var run = new Run(_ => Task.FromResult(Result(null, CustodyOutcome.Appended)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        run.Clock.Advance(Interval - TimeSpan.FromSeconds(1));
        Assert.Equal(1, run.Calls);
        run.Clock.Advance(TimeSpan.FromSeconds(1));
        await run.CallAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(2, run.Calls);
    }

    [Fact]
    public async Task TwoVerificationsNeverRunTogetherWhateverTheClockDoesWhileOneRuns()
    {
        var gate = new TaskCompletionSource<ChainVerification>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var run = new Run(_ => gate.Task);
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.CallAsync(1, TestContext.Current.CancellationToken);

        run.Clock.Advance(Interval * 10);
        Assert.Equal((1, 0), (run.Calls, run.Clock.Waiting));
        gate.SetResult(Result(null, CustodyOutcome.Appended));
        await run.WaitingAsync(TestContext.Current.CancellationToken);
        run.Clock.Advance(Interval);
        await run.CallAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(1, run.MostAtOnce);
    }

    [Fact]
    public async Task ChainThatDoesNotHoldIsAnErrorInTheLogNamingTheFirstInconsistentEventAndWhetherItWasRecordedAndTheServiceGoesOn()
    {
        await using var run = new Run(_ => Task.FromResult(Result(new InconsistentEvent(7, ChainInconsistency.DigestMismatch), CustodyOutcome.Appended)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        run.Clock.Advance(Interval);
        await run.CallAsync(2, TestContext.Current.CancellationToken);

        var line = run.Log.Lines.First(text => text.Contains("sequência 7", StringComparison.Ordinal));
        Assert.StartsWith("Error: ", line, StringComparison.Ordinal);
        Assert.Contains("DigestMismatch", line, StringComparison.Ordinal);
        Assert.Contains("registro do veredito: Appended", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerdictTheChainRefusedIsToldWithTheOutcomeItGot()
    {
        await using var run = new Run(_ => Task.FromResult(Result(new InconsistentEvent(2, ChainInconsistency.SequenceGap), CustodyOutcome.ChainBroken)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        var line = Assert.Single(run.Log.Lines);
        Assert.Contains("registro do veredito: ChainBroken", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChainThatHoldsIsInformationInTheLogWithTheCount()
    {
        await using var run = new Run(_ => Task.FromResult(Result(null, CustodyOutcome.Appended)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        var line = Assert.Single(run.Log.Lines);
        Assert.StartsWith("Information: ", line, StringComparison.Ordinal);
        Assert.Contains("3 eventos lidos", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerificationThatThrowsIsLoggedWithTheTypeOnlyAndTheNextOneRunsAtTheNextInterval()
    {
        var calls = 0;
        await using var run = new Run(_ => ++calls == 1
            ? throw new InvalidOperationException("zqx-texto-da-falha-7731")
            : Task.FromResult(Result(null, CustodyOutcome.Appended)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, run.Clock.Waiting);
        run.Clock.Advance(Interval);
        await run.CallAsync(2, TestContext.Current.CancellationToken);

        var failure = Assert.Single(run.Log.Lines, text => text.StartsWith("Error: ", StringComparison.Ordinal));
        Assert.Contains("InvalidOperationException", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("zqx-texto-da-falha-7731", string.Join('\n', run.Log.Lines), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoppingTheServiceWhileAVerificationRunsEndsWithoutAnErrorInTheLog()
    {
        await using var run = new Run(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Result(null, CustodyOutcome.Appended);
        });
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.CallAsync(1, TestContext.Current.CancellationToken);

        await run.StopAsync();

        Assert.Empty(run.Log.Lines);
        Assert.True(run.Ended);
    }

    [Fact]
    public async Task StoppingTheServiceWhileItWaitsTheIntervalEndsWithoutAnErrorInTheLog()
    {
        await using var run = new Run(_ => Task.FromResult(Result(null, CustodyOutcome.Appended)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        await run.StopAsync();

        Assert.DoesNotContain(run.Log.Lines, text => text.StartsWith("Error: ", StringComparison.Ordinal));
        Assert.True(run.Ended);
    }

    [Fact]
    public async Task ServiceWithNoVerificationToRunDoesNothingAndEndsAtOnce()
    {
        using var log = new RecordedLog();
        using var loggers = new LoggerFactory([log]);
        using var service = new ChainVerificationService((Func<CancellationToken, Task<ChainVerification>>?)null, Schedule, new ManualClock(), loggers);

        await service.StartAsync(TestContext.Current.CancellationToken);
        var executing = service.ExecuteTask!;
        await executing;

        Assert.True(executing.IsCompletedSuccessfully);
    }

    private static ChainVerification Result(InconsistentEvent? first, CustodyOutcome recorded) =>
        new()
        {
            Scope = VerificationScope.Installation,
            EventsRead = 3,
            VerifiedAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
            FirstInconsistent = first,
            Recorded = new CustodyResult { Outcome = recorded },
        };

    /// <summary>One service over a clock and a log of the test's, with the verification it was given counted and its overlap watched.</summary>
    private sealed class Run : IAsyncDisposable
    {
        private readonly Func<CancellationToken, Task<ChainVerification>> verify;
        private readonly SemaphoreSlim started = new(0);
        private readonly ChainVerificationService service;
        private readonly LoggerFactory loggers;
        private int calls;
        private int running;
        private int mostAtOnce;

        public Run(Func<CancellationToken, Task<ChainVerification>> verify)
        {
            this.verify = verify;
            loggers = new LoggerFactory([Log]);
            service = new ChainVerificationService(CountedAsync, Schedule, Clock, loggers);
        }

        public ManualClock Clock { get; } = new();

        public RecordedLog Log { get; } = new();

        public int Calls => Volatile.Read(ref calls);

        public int MostAtOnce => Volatile.Read(ref mostAtOnce);

        public bool Ended => service.ExecuteTask!.IsCompletedSuccessfully;

        public Task StartAsync(CancellationToken cancellationToken) => service.StartAsync(cancellationToken);

        public Task StopAsync() => service.StopAsync(CancellationToken.None);

        /// <summary>Waits until the verification has been called that many times.</summary>
        public async Task CallAsync(int count, CancellationToken cancellationToken)
        {
            while (Calls < count)
            {
                _ = await started.WaitAsync(Patience, cancellationToken);
            }
        }

        /// <summary>Waits until the service waits the interval again, which is when a verification has ended.</summary>
        public async Task WaitingAsync(CancellationToken cancellationToken)
        {
            var until = DateTimeOffset.UtcNow + Patience;
            while (Clock.Waiting == 0 && DateTimeOffset.UtcNow < until)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
            }

            Assert.Equal(1, Clock.Waiting);
        }

        public async ValueTask DisposeAsync()
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
            started.Dispose();
            loggers.Dispose();
        }

        private async Task<ChainVerification> CountedAsync(CancellationToken cancellationToken)
        {
            var now = Interlocked.Increment(ref running);
            _ = Interlocked.Exchange(ref mostAtOnce, Math.Max(MostAtOnce, now));
            _ = Interlocked.Increment(ref calls);
            _ = started.Release();
            try
            {
                return await verify(cancellationToken);
            }
            finally
            {
                _ = Interlocked.Decrement(ref running);
            }
        }
    }
}
