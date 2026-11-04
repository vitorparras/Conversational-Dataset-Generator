using System.Net;
using System.Net.Http.Json;
using Core.Host.Endpoints;
using Core.Persistence.Custody.Verification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The limit of the verification against the seal (issue #637, M4): a session may ask once in each 60 seconds of the clock of the host; the next ask within them is 429, with the time to wait and the
/// reason, and the chain is not read; after them it passes, and another session passes at the same moment. The read of the chain is told by the log of the verification that could not end.
/// </summary>
public sealed class SealVerificationLimitTests
{
    private const string Route = "/custody/verification";
    private const string ChainRead = "contra o selo não chegou ao fim";
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=zqx;Password=zqx;Database=zqx;Timeout=2;Pooling=false";
    private const int TestDeadlineMilliseconds = 60_000;
    private static readonly string Body = $"{{\"seal\":\"{ChainSeal.Of(new DateTimeOffset(2031, 3, 4, 5, 6, 7, TimeSpan.Zero), 5, new byte[32]).ToText()}\"}}";
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    [Fact(Timeout = TestDeadlineMilliseconds)]
    public async Task SessionThatAsksAgainWithinTheWindowIs429WithTheTimeToWaitAndTheChainIsNotReadAndAfterItPassesAsDoesAnotherSession()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new ManualClock();
        var log = new RecordedLog();
        using var loggers = new LoggerFactory([log]);
        await using var source = NpgsqlDataSource.Create(Unreachable);
        using var service = new ChainVerificationService(null, new VerificationSchedule(TimeSpan.FromHours(1)), clock, loggers) { Verifier = new CustodyVerifier(source, clock) };
        await using var host = await SessionedHost.StartAsync(
            services => services.AddSingleton(service).AddSingleton(new SealDeclaration(SealSituation.External, null)).AddSingleton<TimeProvider>(clock).AddSingleton<ILoggerFactory>(loggers),
            SealVerificationEndpoint.Map,
            token);
        var session = host.SeedSession();

        using var first = await host.PostAsync(Route, Body, session, token);
        using var second = await host.PostAsync(Route, Body, session, token);
        var refused = await second.Content.ReadFromJsonAsync<VerificationLimited>(token);
        using var other = await host.PostAsync(Route, Body, host.SeedSession(), token);
        var readAfterTheSecond = log.Lines.Count(line => line.Contains(ChainRead, StringComparison.Ordinal));
        clock.Advance(Window - TimeSpan.FromSeconds(1));
        using var early = await host.PostAsync(Route, Body, session, token);
        clock.Advance(TimeSpan.FromSeconds(1));
        using var after = await host.PostAsync(Route, Body, session, token);

        Assert.Equal(
            (HttpStatusCode.ServiceUnavailable, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable),
            (first.StatusCode, second.StatusCode, other.StatusCode, early.StatusCode, after.StatusCode));
        Assert.Equal((2, 3), (readAfterTheSecond, log.Lines.Count(line => line.Contains(ChainRead, StringComparison.Ordinal))));
        Assert.Equal((60, 1), (WaitAdvised(second), WaitAdvised(early)));
        Assert.Equal(60, refused!.RetryAfterSeconds);
        Assert.Contains("a cadeia não foi lida", refused.Reason, StringComparison.Ordinal);
        Assert.Null(after.Headers.RetryAfter);
    }

    [Fact(Timeout = TestDeadlineMilliseconds)]
    public async Task TextThatIsNotASealIs400AndDoesNotSpendTheSlotOfTheSession()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new ManualClock();
        using var loggers = new LoggerFactory();
        using var service = new ChainVerificationService(null, new VerificationSchedule(TimeSpan.FromHours(1)), clock, loggers);
        await using var host = await SessionedHost.StartAsync(
            services => services.AddSingleton(service).AddSingleton(new SealDeclaration(SealSituation.External, null)).AddSingleton<TimeProvider>(clock).AddSingleton<ILoggerFactory>(loggers),
            SealVerificationEndpoint.Map,
            token);
        var session = host.SeedSession();

        using var notASeal = await host.PostAsync(Route, "{\"seal\":\"zqx\"}", session, token);
        using var asked = await host.PostAsync(Route, Body, session, token);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.ServiceUnavailable), (notASeal.StatusCode, asked.StatusCode));
    }

    private static int WaitAdvised(HttpResponseMessage response) => (int)response.Headers.RetryAfter!.Delta!.Value.TotalSeconds;

    [Fact]
    public void SessionIsAdmittedOnceAndThenToldWhatIsLeftOfTheWindowUntilItEndsExactlyAtItsLength()
    {
        var limit = new VerificationLimit();
        var start = new DateTimeOffset(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);

        var first = limit.Admit("a", start);
        var again = limit.Admit("a", start.AddSeconds(20));
        var other = limit.Admit("b", start.AddSeconds(20));
        var justBefore = limit.Admit("a", start + Window - TimeSpan.FromMilliseconds(1));
        var atTheEnd = limit.Admit("a", start + Window);

        Assert.Equal((null, TimeSpan.FromSeconds(40), null, TimeSpan.FromMilliseconds(1), null), (first, again, other, justBefore, atTheEnd));
    }

    [Fact]
    public void SessionAdmittedAtTheEndOfTheWindowStartsAnotherWindowFromThatMoment()
    {
        var limit = new VerificationLimit();
        var start = new DateTimeOffset(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);
        _ = limit.Admit("a", start);
        _ = limit.Admit("a", start + Window);

        var inside = limit.Admit("a", start + Window + TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), inside);
    }
}
