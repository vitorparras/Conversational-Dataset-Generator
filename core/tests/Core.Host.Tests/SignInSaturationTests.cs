using System.Diagnostics.CodeAnalysis;
using System.Net;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The limits of the answers that wait (#71, decision 0048): 4 for each network prefix and 64 for the host. At a limit the entry is refused before the password is checked, with the answer
/// of any refusal, so a full limit is neither a cheap attempt nor a verdict. The check of the password is a database that does not exist: reaching it fails, not reaching it answers.
/// </summary>
public sealed class SignInSaturationTests
{
    private static readonly IPAddress Here = SignInDelayTests.NetworkNumber(1);

    [Fact]
    public async Task FourAnswersWaitingForOnePrefixSaturateThatPrefixOnlyAndTheFifthComesBackAtOnce()
    {
        var (clock, delay) = Made();
        _ = delay.RefusedAsync("a-0", Here, TestContext.Current.CancellationToken);
        var waiting = Enumerable.Range(1, SignInDelay.MaxWaitingPerOrigin)
            .Select(index => delay.RefusedAsync($"a-{index}", Here, TestContext.Current.CancellationToken)).ToList();
        Assert.Equal(SignInDelay.MaxWaitingPerOrigin, clock.Waiting);

        var beyond = delay.RefusedAsync("a-5", Here, TestContext.Current.CancellationToken);

        Assert.True(beyond.IsCompletedSuccessfully);
        Assert.True(delay.Saturated(Here));
        Assert.False(delay.Saturated(SignInDelayTests.NetworkNumber(2)));
        clock.Advance(SignInDelay.Ceiling);
        await Task.WhenAll(waiting);
        Assert.False(delay.Saturated(Here));
    }

    [Fact]
    public async Task SixtyFourAnswersWaitingSaturateTheHostWhateverTheOriginAndTheNextComesBackAtOnce()
    {
        var (clock, delay) = Made();
        _ = delay.RefusedAsync("ana", Here, TestContext.Current.CancellationToken);
        var waiting = Enumerable.Range(2, SignInDelay.MaxWaiting)
            .Select(index => delay.RefusedAsync("ana", SignInDelayTests.NetworkNumber(index), TestContext.Current.CancellationToken)).ToList();
        Assert.Equal(SignInDelay.MaxWaiting, clock.Waiting);

        var beyond = delay.RefusedAsync("ana", SignInDelayTests.NetworkNumber(500), TestContext.Current.CancellationToken);

        Assert.True(beyond.IsCompletedSuccessfully);
        Assert.Equal(SignInDelay.MaxWaiting, clock.Waiting);
        Assert.True(delay.Saturated(SignInDelayTests.NetworkNumber(900)));
        clock.Advance(SignInDelay.Ceiling);
        await Task.WhenAll(waiting);
        Assert.False(delay.Saturated(SignInDelayTests.NetworkNumber(900)));
    }

    [Fact]
    public void AnEntryThatIsNotCheckedStillCountsAsARefusalSoTheDelayKeepsRising()
    {
        var (_, delay) = Made();

        for (var refusal = 0; refusal < 8; refusal++)
        {
            delay.Counted("ana", Here);
        }

        var next = delay.RefusedAsync("ana", Here, TestContext.Current.CancellationToken);
        Assert.False(next.IsCompleted);
    }

    [Fact]
    public async Task AtTheLimitOfThePrefixTheEntryIsRefusedWithoutCheckingThePasswordWhateverThePassword()
    {
        var (clock, delay) = Made();
        var signIn = Unreachable(clock, delay);
        _ = delay.RefusedAsync("a-0", Here, TestContext.Current.CancellationToken);
        _ = Enumerable.Range(1, SignInDelay.MaxWaitingPerOrigin)
            .Select(index => delay.RefusedAsync($"a-{index}", Here, TestContext.Current.CancellationToken)).ToList();

        var rightOrWrong = await signIn.OpenAsync("ana", "a senha certa ou errada", Here, TestContext.Current.CancellationToken);

        Assert.Null(rightOrWrong);
    }

    [Fact]
    public async Task AtTheLimitOfTheHostTheEntryOfAnyOriginIsRefusedWithoutCheckingThePassword()
    {
        var (clock, delay) = Made();
        var signIn = Unreachable(clock, delay);
        _ = delay.RefusedAsync("ana", Here, TestContext.Current.CancellationToken);
        _ = Enumerable.Range(2, SignInDelay.MaxWaiting)
            .Select(index => delay.RefusedAsync("ana", SignInDelayTests.NetworkNumber(index), TestContext.Current.CancellationToken)).ToList();

        var fromElsewhere = await signIn.OpenAsync("bia", "a senha certa ou errada", SignInDelayTests.NetworkNumber(700), TestContext.Current.CancellationToken);

        Assert.Null(fromElsewhere);
    }

    [Fact]
    public async Task BelowTheLimitsTheEntryReachesTheCheckOfThePassword()
    {
        var (clock, delay) = Made();
        var signIn = Unreachable(clock, delay);

        _ = await Assert.ThrowsAnyAsync<Exception>(() => signIn.OpenAsync("ana", "senha", Here, TestContext.Current.CancellationToken));
    }

    private static (ManualClock Clock, SignInDelay Delay) Made()
    {
        var clock = new ManualClock();
        return (clock, new SignInDelay(clock));
    }

    /// <summary>An entry whose database does not answer: whatever reaches it throws, so an answer without an exception never reached it.</summary>
    [SuppressMessage("Reliability", "CA2000", Justification = "The data source never connects, so it holds nothing to release.")]
    private static SignIn Unreachable(TimeProvider clock, SignInDelay delay)
    {
        var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1;Command Timeout=1;Pooling=false");
        var sessions = new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), clock);
        return new SignIn(new PersonRegistry(source), new PersonCredentials(source, new PasswordDerivation(1)), sessions, delay);
    }
}
