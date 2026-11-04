using Core.Boundary.Http;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>
/// The service that sends the seal of the chain (issue #62, part B2a): the seal of the tip is sent and only a seal that arrived is recorded, as the seal that was sent; a failure of any kind
/// writes nothing and is told in the log with its reason and the count, never the seal; the manual situation sends nothing; and the next try waits the interval, also after a restart.
/// </summary>
public sealed class SealSendingServiceTests
{
    private static readonly TimeSpan Interval = SealSendingService.Interval;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task SealThatArrivedIsRecordedAsTheSealThatWasSentEvenIfTheChainGrewMeanwhile()
    {
        var fakes = new Fakes();
        fakes.Send = _ =>
        {
            fakes.Chain = 9;
            return Task.FromResult(SealDeliveryResult.Arrived);
        };
        await using var run = new Run(fakes);
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        var recorded = Assert.Single(fakes.Recorded);
        Assert.Equal((5L, 5L, 1), (recorded.Events, ChainSeal.Read(fakes.Sent[0]).Seal!.Events, fakes.Sent.Count));
        Assert.Equal(fakes.Sent[0], recorded.ToText());
        Assert.Contains(run.Log.Lines, line => line.Contains("enviado e registrado: 5 eventos", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SealThatDidNotArriveIsNotRecordedAndTheLogNamesTheReasonAndTheCountButNotTheSeal()
    {
        var fakes = new Fakes { Send = _ => Task.FromResult(new SealDeliveryResult(SealDeliveryFailure.DestinationAnswered, 500)) };
        await using var run = new Run(fakes);
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        var line = Assert.Single(run.Log.Lines, text => text.Contains("NÃO enviado", StringComparison.Ordinal));
        Assert.Empty(fakes.Recorded);
        Assert.Contains("DestinationAnswered (resposta 500)", line, StringComparison.Ordinal);
        Assert.Contains("5 eventos", line, StringComparison.Ordinal);
        Assert.DoesNotContain("CDG-SELO", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SealThatArrivedButWasRefusedByTheChainIsLoggedWithTheNameOfTheOutcomeAndTheCount()
    {
        var fakes = new Fakes { Outcome = CustodyOutcome.SealNotOfTheChain };
        await using var run = new Run(fakes);
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        var line = Assert.Single(run.Log.Lines, text => text.Contains("registro foi recusado", StringComparison.Ordinal));
        Assert.Contains("(SealNotOfTheChain)", line, StringComparison.Ordinal);
        Assert.Contains("5 eventos", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendingThatThrowsRecordsNothingIsLoggedWithTheTypeOfTheFailureAndTheServiceTriesAgainAtTheNextInterval()
    {
        var fakes = new Fakes { Send = _ => throw new InvalidOperationException("zqx") };
        await using var run = new Run(fakes);
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        fakes.Send = _ => Task.FromResult(SealDeliveryResult.Arrived);
        run.Clock.Advance(Interval);
        await run.RecordedAsync(1, TestContext.Current.CancellationToken);

        Assert.Contains(run.Log.Lines, line => line.Contains("InvalidOperationException", StringComparison.Ordinal) && !line.Contains("zqx", StringComparison.Ordinal));
        Assert.Single(fakes.Recorded);
    }

    [Fact]
    public async Task NoStepsMeansNothingIsSentAndTheServiceEnds()
    {
        using var loggers = new LoggerFactory();
        using var service = new SealSendingService(null, new ManualClock(), loggers);

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ManualSituationBuildsNoSendingAndSendsNothing()
    {
        using var startup = new ServiceCollection().BuildServiceProvider();
        using var loggers = new LoggerFactory();
        using var host = new ServiceCollection()
            .AddSingleton(new SealDeclaration(SealSituation.Manual, null))
            .AddSingleton(startup)
            .AddSingleton<TimeProvider>(new ManualClock())
            .AddSingleton<ILoggerFactory>(loggers)
            .BuildServiceProvider();
        using var service = new SealSendingService(host);

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task NextSendingWaitsTheWholeIntervalAndNotLess()
    {
        var fakes = new Fakes();
        await using var run = new Run(fakes);
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        run.Clock.Advance(Interval - TimeSpan.FromSeconds(1));
        Assert.Single(fakes.Recorded);
        run.Clock.Advance(TimeSpan.FromSeconds(1));
        await run.RecordedAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(2, fakes.Recorded.Count);
    }

    [Fact]
    public async Task StartAfterARecentSealWaitsWhatIsLeftOfTheIntervalBeforeSending()
    {
        var fakes = new Fakes();
        await using var run = new Run(fakes);
        fakes.Last = run.Clock.GetUtcNow() - TimeSpan.FromHours(6);
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);
        Assert.Equal((0, (TimeSpan?)(Interval - TimeSpan.FromHours(6))), (fakes.Sent.Count, run.Clock.NextDue));

        run.Clock.Advance(Interval - TimeSpan.FromHours(6));
        await run.RecordedAsync(1, TestContext.Current.CancellationToken);

        Assert.Single(fakes.Recorded);
    }

    [Fact]
    public async Task StartAfterASealOlderThanTheIntervalSendsAtOnce()
    {
        var fakes = new Fakes();
        await using var run = new Run(fakes);
        fakes.Last = run.Clock.GetUtcNow() - Interval - TimeSpan.FromHours(1);
        await run.StartAsync(TestContext.Current.CancellationToken);
        await run.WaitingAsync(TestContext.Current.CancellationToken);

        Assert.Single(fakes.Recorded);
    }

    private sealed class Fakes
    {
        private static readonly DateTimeOffset Moment = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);

        public long Chain { get; set; } = 5;

        public CustodyOutcome Outcome { get; set; } = CustodyOutcome.Appended;

        public DateTimeOffset? Last { get; set; }

        public Func<string, Task<SealDeliveryResult>> Send { get; set; } = _ => Task.FromResult(SealDeliveryResult.Arrived);

        public List<string> Sent { get; } = [];

        public List<ChainSeal> Recorded { get; } = [];

        public SealSteps Steps() => new(MakeAsync, SendAsync, RecordAsync, _ => Task.FromResult(Last));

        private Task<ChainSeal> MakeAsync(CancellationToken cancellationToken) => Task.FromResult(ChainSeal.Of(Moment, Chain, new byte[32]));

        private async Task<SealDeliveryResult> SendAsync(string text, CancellationToken cancellationToken)
        {
            Sent.Add(text);
            return await Send(text);
        }

        private Task<SealRecording> RecordAsync(ChainSeal seal, CancellationToken cancellationToken)
        {
            Recorded.Add(seal);
            var appended = Outcome == CustodyOutcome.Appended;
            return Task.FromResult(new SealRecording { Recorded = new CustodyResult { Outcome = Outcome, Sequence = appended ? 9 : null }, Seal = appended ? seal : null });
        }
    }

    private sealed class Run : IAsyncDisposable
    {
        private readonly Fakes fakes;
        private readonly LoggerFactory loggers;
        private readonly SealSendingService service;

        public Run(Fakes fakes)
        {
            this.fakes = fakes;
            loggers = new LoggerFactory([Log]);
            service = new SealSendingService(fakes.Steps(), Clock, loggers);
        }

        public ManualClock Clock { get; } = new();

        public RecordedLog Log { get; } = new();

        public Task StartAsync(CancellationToken cancellationToken) => service.StartAsync(cancellationToken);

        /// <summary>Waits until the service waits again, which is when a sending has ended.</summary>
        public async Task WaitingAsync(CancellationToken cancellationToken)
        {
            var until = DateTimeOffset.UtcNow + Patience;
            while (Clock.Waiting == 0 && DateTimeOffset.UtcNow < until)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
            }

            Assert.Equal(1, Clock.Waiting);
        }

        /// <summary>Waits until that many seals were recorded.</summary>
        public async Task RecordedAsync(int count, CancellationToken cancellationToken)
        {
            var until = DateTimeOffset.UtcNow + Patience;
            while (fakes.Recorded.Count < count && DateTimeOffset.UtcNow < until)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
            loggers.Dispose();
        }
    }
}
