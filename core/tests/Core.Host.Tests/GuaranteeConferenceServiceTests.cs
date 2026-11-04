using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.ServerGuarantees;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>
/// The conference of the guarantees of the server in the round of the verification of the chain (issue #68): it runs after the chain, at the start and then every interval, never together
/// with it; what is missing is told in the log by name as an error, what holds is information, and a conference that does not end is told by the type of the failure and tried again.
/// </summary>
public sealed class GuaranteeConferenceServiceTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ConferenceRunsAfterTheChainAtTheStartAndAgainAtTheNextInterval()
    {
        var order = new List<string>();
        await using var run = new Run(order, _ => Task.FromResult(Verification(GuaranteeKinds.None)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        run.Clock.Advance(Interval);
        await run.WaitingAsync(TestContext.Current.CancellationToken, round: 2);

        Assert.Equal(["chain", "guarantees", "chain", "guarantees"], order);
    }

    [Fact]
    public async Task GuaranteesMissingAreAnErrorInTheLogNamingEachOneAndTheOutcomeOfTheRecord()
    {
        await using var run = new Run([], _ => Task.FromResult(Verification(GuaranteeKinds.RowPolicy | GuaranteeKinds.Immutability)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        var line = Assert.Single(run.Log.Lines, text => text.Contains("garantias", StringComparison.Ordinal));
        Assert.StartsWith("Error: ", line, StringComparison.Ordinal);
        Assert.Contains("FALTAM RowPolicy, Immutability", line, StringComparison.Ordinal);
        Assert.Contains("registro do veredito: Appended", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GuaranteesThatHoldAreInformationInTheLog()
    {
        await using var run = new Run([], _ => Task.FromResult(Verification(GuaranteeKinds.None)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        var line = Assert.Single(run.Log.Lines, text => text.Contains("garantias", StringComparison.Ordinal));
        Assert.StartsWith("Information: ", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConferenceThatThrowsIsLoggedWithTheTypeOnlyAndTheNextOneRunsAtTheNextInterval()
    {
        var calls = 0;
        await using var run = new Run([], _ => ++calls == 1 ? throw new InvalidOperationException("zqx-texto-da-falha-4417") : Task.FromResult(Verification(GuaranteeKinds.None)));
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        run.Clock.Advance(Interval);
        await run.WaitingAsync(TestContext.Current.CancellationToken, round: 2);

        var failure = Assert.Single(run.Log.Lines, text => text.StartsWith("Error: ", StringComparison.Ordinal));
        Assert.Contains("InvalidOperationException", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("zqx-texto-da-falha-4417", string.Join('\n', run.Log.Lines), StringComparison.Ordinal);
        Assert.Equal(2, calls);
    }

    private static GuaranteeVerification Verification(GuaranteeKinds missing) => new(missing, new CustodyResult { Outcome = CustodyOutcome.Appended });

    /// <summary>One service over a clock and a log of the test's, with the order in which the chain and the conference were called.</summary>
    private sealed class Run : IAsyncDisposable
    {
        private readonly ChainVerificationService service;
        private readonly LoggerFactory loggers;
        private int conferences;

        public Run(List<string> order, Func<CancellationToken, Task<GuaranteeVerification>> confer)
        {
            loggers = new LoggerFactory([Log]);
            service = new ChainVerificationService(
                _ =>
                {
                    order.Add("chain");
                    return Task.FromResult(Chain());
                },
                new VerificationSchedule(Interval),
                Clock,
                loggers)
            {
                Guarantees = token =>
                {
                    order.Add("guarantees");
                    _ = Interlocked.Increment(ref conferences);
                    return confer(token);
                },
            };
        }

        public ManualClock Clock { get; } = new();

        public RecordedLog Log { get; } = new();

        public Task StartAsync(CancellationToken cancellationToken) => service.StartAsync(cancellationToken);

        /// <summary>Waits until the service waits the interval for the given time, which is when both the chain and the conference of that round have ended.</summary>
        public async Task WaitingAsync(CancellationToken cancellationToken, int round = 1)
        {
            var until = DateTimeOffset.UtcNow + Patience;
            while ((Clock.Waiting == 0 || Volatile.Read(ref conferences) < round) && DateTimeOffset.UtcNow < until)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
            }

            Assert.Equal(1, Clock.Waiting);
        }

        public async ValueTask DisposeAsync()
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
            loggers.Dispose();
        }

        private static ChainVerification Chain() =>
            new()
            {
                Scope = VerificationScope.Installation,
                EventsRead = 3,
                VerifiedAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
                Recorded = new CustodyResult { Outcome = CustodyOutcome.Appended },
            };
    }
}
