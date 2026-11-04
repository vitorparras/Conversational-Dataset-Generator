using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>
/// The warning of the seal that the periodic service keeps for /status (issue #62, part B2b, criterion 3): after a round it holds the warning for no seal and for a seal of 8 days, holds none for
/// a seal of 6 days, takes the record of a new seal at once when asked to read again, and a service with nothing to read has none.
/// </summary>
public sealed class SealWarningTests
{
    private static readonly VerificationSchedule Schedule = new(TimeSpan.FromMinutes(5));
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData(null, true)]
    [InlineData(-8, true)]
    [InlineData(-6, false)]
    public async Task TheRoundLeavesTheWarningForNoSealAndForASealOfEightDaysButNotForOneOfSixDays(int? days, bool warns)
    {
        var clock = new ManualClock();
        using var log = new RecordedLog();
        using var loggers = new LoggerFactory([log]);
        DateTimeOffset? last = days is { } age ? clock.GetUtcNow().AddDays(age) : null;
        using var service = new ChainVerificationService(
            _ => Task.FromResult(Result()), Schedule, clock, loggers, _ => Task.FromResult(SealAge.Warning(SealSituation.Manual, last, clock.GetUtcNow())), new SealWarningState());

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitingAsync(clock, TestContext.Current.CancellationToken);

        Assert.Equal(warns, service.SealWarning.Current is not null);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AVerificationThatThrowsStillRenewsTheWarningOfTheRound()
    {
        var clock = new ManualClock();
        using var log = new RecordedLog();
        using var loggers = new LoggerFactory([log]);
        using var service = new ChainVerificationService(
            _ => throw new InvalidOperationException("zqx"), Schedule, clock, loggers, _ => Task.FromResult<string?>("avisar"), new SealWarningState());

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitingAsync(clock, TestContext.Current.CancellationToken);

        Assert.Equal("avisar", service.SealWarning.Current);
        Assert.Contains(log.Lines, line => line.Contains("InvalidOperationException", StringComparison.Ordinal));
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ARenewalThatFailsIsToldWithItsOwnTextAndKeepsTheWarningItHad()
    {
        var clock = new ManualClock();
        using var log = new RecordedLog();
        using var loggers = new LoggerFactory([log]);
        using var service = new ChainVerificationService(
            _ => Task.FromResult(Result()), Schedule, clock, loggers, _ => throw new InvalidOperationException("zqx"), new SealWarningState());
        service.SealWarning.Record("avisar");

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitingAsync(clock, TestContext.Current.CancellationToken);

        Assert.Equal("avisar", service.SealWarning.Current);
        var line = Assert.Single(log.Lines, text => text.Contains("renovação do aviso do selo", StringComparison.Ordinal));
        Assert.Contains("InvalidOperationException", line, StringComparison.Ordinal);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReadingAgainAfterASealWasRecordedClearsTheWarningWithoutWaitingTheInterval()
    {
        var clock = new ManualClock();
        DateTimeOffset? last = null;
        using var loggers = new LoggerFactory();
        using var service = new ChainVerificationService(
            _ => Task.FromResult(Result()), Schedule, clock, loggers, _ => Task.FromResult(SealAge.Warning(SealSituation.Manual, last, clock.GetUtcNow())), new SealWarningState());
        await service.RefreshSealWarningAsync(TestContext.Current.CancellationToken);
        var before = service.SealWarning.Current;

        last = clock.GetUtcNow();
        await service.RefreshSealWarningAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(before);
        Assert.Null(service.SealWarning.Current);
    }

    [Fact]
    public async Task AServiceWithNothingToReadHasNoWarning()
    {
        using var loggers = new LoggerFactory();
        using var service = new ChainVerificationService(null, Schedule, new ManualClock(), loggers);

        await service.RefreshSealWarningAsync(TestContext.Current.CancellationToken);

        Assert.Null(service.SealWarning.Current);
    }

    private static ChainVerification Result() =>
        new()
        {
            Scope = VerificationScope.Installation,
            EventsRead = 3,
            VerifiedAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
            FirstInconsistent = null,
            Recorded = new CustodyResult { Outcome = CustodyOutcome.Appended },
        };

    private static async Task WaitingAsync(ManualClock clock, CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + Patience;
        while (clock.Waiting == 0 && DateTimeOffset.UtcNow < until)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        }

        Assert.Equal(1, clock.Waiting);
    }
}
