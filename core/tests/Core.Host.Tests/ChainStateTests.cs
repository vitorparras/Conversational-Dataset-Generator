using Core.Host.Endpoints;
using Core.Persistence.Custody.Verification;
using Core.Platform.Sessions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// What the route of the chain and the screen of the chain share (#605): the reading answers nothing where there is no database, and a session asks for a verification once in each window,
/// the refusal telling the seconds to wait before the chain is read; the asker the verification records is the person of the session.
/// </summary>
public sealed class ChainStateTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task WithNoDatabaseThereIsNothingToReadAndNothingToVerify()
    {
        var token = TestContext.Current.CancellationToken;
        using var loggers = new LoggerFactory();
        using var service = new ChainVerificationService(null, new VerificationSchedule(TimeSpan.FromHours(1)), new ManualClock(), loggers);
        var sources = Sources(service, new ManualClock(), loggers);

        var shown = await ChainState.ReadAsync(sources, token);
        var asked = await ChainState.VerifyAsync(sources, "session", seal: null, token);

        Assert.Null(shown);
        Assert.Equal((null, null), (asked.Done, asked.WaitSeconds));
    }

    [Fact]
    public async Task ASessionThatAsksAgainWithinTheWindowIsToldTheSecondsToWaitAndAfterItPassesAndAnotherSessionAlwaysDoes()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new ManualClock();
        using var loggers = new LoggerFactory();
        await using var source = NpgsqlDataSource.Create(UnreachableDatabase.ConnectionString);
        using var service = new ChainVerificationService(null, new VerificationSchedule(TimeSpan.FromHours(1)), clock, loggers) { Verifier = new CustodyVerifier(source, clock) };
        var sources = Sources(service, clock, loggers);

        var first = await ChainState.VerifyAsync(sources, "one", seal: null, token);
        var again = await ChainState.VerifyAsync(sources, "one", seal: null, token);
        var other = await ChainState.VerifyAsync(sources, "two", seal: null, token);
        clock.Advance(Window - TimeSpan.FromSeconds(1));
        var early = await ChainState.VerifyAsync(sources, "one", seal: null, token);
        clock.Advance(TimeSpan.FromSeconds(1));
        var after = await ChainState.VerifyAsync(sources, "one", seal: null, token);

        Assert.Equal<int?>([null, 60, null, 1, null], new[] { first, again, other, early, after }.Select(asked => asked.WaitSeconds));
    }

    private static ChainSources Sources(ChainVerificationService service, TimeProvider clock, ILoggerFactory loggers) =>
        new(service, new SealDeclaration(SealSituation.External, null), new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), clock), clock, loggers);
}
