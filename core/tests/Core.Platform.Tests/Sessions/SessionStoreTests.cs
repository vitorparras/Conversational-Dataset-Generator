using Core.Platform.Sessions;

namespace Core.Platform.Tests.Sessions;

/// <summary>The two limits and the end of a session, kept at the server (issue #70, CA-1 to CA-3), with a clock the test moves.</summary>
public sealed class SessionStoreTests
{
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Absolute = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    private readonly AdjustableClock clock = new();
    private readonly SessionStore store;

    public SessionStoreTests()
    {
        store = new SessionStore(new SessionLifetimes(Idle, Absolute), clock);
    }

    [Fact]
    public void SessionOpenedIsResolvedToThePersonWhoEntered()
    {
        var identifier = store.Open(7);

        Assert.Equal(7, store.Resolve(identifier));
    }

    [Fact]
    public void IdentifierIs256BitsWrittenInHexadecimal()
    {
        var identifier = store.Open(1);

        Assert.Equal(64, identifier.Length);
        Assert.All(identifier, character => Assert.True(char.IsAsciiHexDigit(character)));
    }

    [Fact]
    public void IdentifierIsNeverRepeated() => Assert.NotEqual(store.Open(1), store.Open(1));

    [Fact]
    public void SessionWithoutActivityForMoreThanTheIdleLimitIsRefusedWithTheIdentifierIntact()
    {
        var identifier = store.Open(7);

        clock.Advance(Idle + Tick);

        Assert.Null(store.Resolve(identifier));
    }

    [Fact]
    public void SessionWithoutActivityForExactlyTheIdleLimitIsStillAccepted()
    {
        var identifier = store.Open(7);

        clock.Advance(Idle);

        Assert.Equal(7, store.Resolve(identifier));
    }

    [Fact]
    public void ActivityStartsTheIdleLimitAgain()
    {
        var identifier = store.Open(7);
        clock.Advance(Idle - Tick);
        _ = store.Resolve(identifier);

        clock.Advance(Idle - Tick);

        Assert.Equal(7, store.Resolve(identifier));
    }

    [Fact]
    public void SessionInContinuousUseIsRefusedOnceTheAbsoluteLimitPasses()
    {
        var identifier = store.Open(7);
        UseAfter(identifier, TimeSpan.FromMinutes(9));
        UseAfter(identifier, TimeSpan.FromMinutes(9));
        UseAfter(identifier, TimeSpan.FromMinutes(9));
        clock.Advance(TimeSpan.FromMinutes(3));
        var atTheLimit = store.Resolve(identifier);

        clock.Advance(Tick);

        Assert.Equal(7, atTheLimit);
        Assert.Null(store.Resolve(identifier));
    }

    [Fact]
    public void ActivityDoesNotRenewTheAbsoluteLimit()
    {
        var identifier = store.Open(7);
        UseAfter(identifier, TimeSpan.FromMinutes(9));
        UseAfter(identifier, TimeSpan.FromMinutes(9));
        UseAfter(identifier, TimeSpan.FromMinutes(9));

        clock.Advance(TimeSpan.FromMinutes(3) + Tick);

        Assert.Null(store.Resolve(identifier));
    }

    [Fact]
    public void SessionClosedByWhoUsedItIsRefusedWhenTheSameIdentifierIsPresentedAgain()
    {
        var identifier = store.Open(7);

        store.Close(identifier);

        Assert.Null(store.Resolve(identifier));
    }

    [Fact]
    public void ClosingASessionLeavesTheOthersOfTheSamePersonAlone()
    {
        var closed = store.Open(7);
        var kept = store.Open(7);

        store.Close(closed);

        Assert.Equal(7, store.Resolve(kept));
    }

    [Fact]
    public void IdentifierTheStoreNeverIssuedIsRefused()
    {
        _ = store.Open(7);

        Assert.Null(store.Resolve(new string('0', 64)));
    }

    [Fact]
    public void NoIdentifierIsRefused()
    {
        _ = store.Open(7);

        Assert.Null(store.Resolve(null));
    }

    [Fact]
    public void ClosingNoIdentifierChangesNothing()
    {
        var identifier = store.Open(7);

        store.Close(null);

        Assert.Equal(7, store.Resolve(identifier));
    }

    [Fact]
    public void ClosingAnIdentifierTheStoreDoesNotKnowChangesNothing()
    {
        var identifier = store.Open(7);

        store.Close(new string('0', 64));

        Assert.Equal(7, store.Resolve(identifier));
    }

    [Fact]
    public void SessionRefusedForPassingALimitIsForgottenAndStaysRefusedWhenTheClockIsBack()
    {
        var identifier = store.Open(7);
        clock.Advance(Idle + Tick);
        _ = store.Resolve(identifier);

        clock.Advance(-Idle);

        Assert.Null(store.Resolve(identifier));
    }

    [Fact]
    public void OpeningASessionForgetsTheOnesThatPassedALimitAndKeepsTheLive()
    {
        var passed = store.Open(1);
        clock.Advance(Idle + Tick);
        var live = store.Open(2);

        clock.Advance(-Idle);

        Assert.Null(store.Resolve(passed));
        Assert.Equal(2, store.Resolve(live));
    }

    [Fact]
    public void AStoreMadeAfterARestartDoesNotKnowTheSessionsOfTheOneBefore()
    {
        var identifier = store.Open(7);
        var restarted = new SessionStore(new SessionLifetimes(Idle, Absolute), clock);

        Assert.Null(restarted.Resolve(identifier));
    }

    [Fact]
    public async Task ManyThreadsOpeningAndResolvingKeepEverySessionAccepted()
    {
        var opened = await Task.WhenAll(Enumerable.Range(1, 64).Select(person => Task.Run(() => (Person: (long)person, Identifier: store.Open(person)))));

        var resolved = await Task.WhenAll(opened.Select(entry => Task.Run(() => (entry.Person, Found: store.Resolve(entry.Identifier)))));

        Assert.All(resolved, entry => Assert.Equal(entry.Person, entry.Found));
    }

    private void UseAfter(string identifier, TimeSpan time)
    {
        clock.Advance(time);
        Assert.Equal(7, store.Resolve(identifier));
    }
}
