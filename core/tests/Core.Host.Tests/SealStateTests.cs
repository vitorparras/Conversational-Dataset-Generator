using Core.Host.Endpoints;
using Core.Persistence.Custody.Verification;
using Core.Platform.Sessions;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>
/// What the route of the seal and the screen of the seal share (#736), with no database: the seal is operated by hand only when the installation declared Manual and has a chain to read, and
/// the record of a seal says a text that is not a seal, and a session that is gone, before anything is read from the chain.
/// </summary>
public sealed class SealStateTests
{
    private static readonly DateTimeOffset Moment = new(2031, 3, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheSealIsOperatedByHandOnlyInTheManualSituationAndWithAChainToRead()
    {
        (SealSituation Situation, SealAvailability Expected)[] cases =
        [
            (SealSituation.External, SealAvailability.NotManual),
            (SealSituation.Internal, SealAvailability.NotManual),
            (SealSituation.Manual, SealAvailability.Unreadable),
        ];
        using var loggers = new LoggerFactory();
        using var service = new ChainVerificationService(null, new VerificationSchedule(TimeSpan.FromHours(1)), new ManualClock(), loggers);

        var availabilities = cases.Select(c => SealState.Availability(Sources(service, c.Situation, loggers))).ToArray();

        Assert.Equal(cases.Select(c => c.Expected).ToArray(), availabilities);
    }

    [Theory]
    [InlineData("")]
    [InlineData("isto não é um selo")]
    public async Task ATextThatIsNotASealIsToldApartAndGivesItsReasonBeforeTheSessionOrTheChainIsLooked(string text)
    {
        using var loggers = new LoggerFactory();
        using var service = new ChainVerificationService(null, new VerificationSchedule(TimeSpan.FromHours(1)), new ManualClock(), loggers);

        var confirmation = await SealState.ConfirmAsync(Sources(service, SealSituation.Manual, loggers), "no-session", text, TestContext.Current.CancellationToken);

        Assert.Equal(SealConfirmationOutcome.NotASeal, confirmation.Outcome);
        Assert.NotEmpty(confirmation.Reason);
        Assert.Null(confirmation.Recorded);
    }

    [Fact]
    public async Task ASealReadFromASessionThatIsGoneIsToldApartAndNothingIsRecorded()
    {
        using var loggers = new LoggerFactory();
        using var service = new ChainVerificationService(null, new VerificationSchedule(TimeSpan.FromHours(1)), new ManualClock(), loggers);
        var text = ChainSeal.Of(Moment, 3, new byte[ChainDigest.Length]).ToText();

        var confirmation = await SealState.ConfirmAsync(Sources(service, SealSituation.Manual, loggers), "no-session", text, TestContext.Current.CancellationToken);

        Assert.Equal((SealConfirmationOutcome.NoSession, null), (confirmation.Outcome, confirmation.Recorded));
    }

    private static ChainSources Sources(ChainVerificationService service, SealSituation situation, ILoggerFactory loggers)
    {
        var clock = new ManualClock();
        var destination = situation == SealSituation.Manual ? null : new Uri("https://selo.example.test/");
        return new(service, new SealDeclaration(situation, destination), new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), clock), clock, loggers);
    }
}
