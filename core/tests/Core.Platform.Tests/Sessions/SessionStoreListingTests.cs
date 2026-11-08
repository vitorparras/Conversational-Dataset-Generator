using Core.Platform.Sessions;

namespace Core.Platform.Tests.Sessions;

/// <summary>Listing and ending the sessions of one person (issues 597 and 598): the listing names a session without being its identifier, and nobody reaches another person's.</summary>
public sealed class SessionStoreListingTests
{
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Absolute = TimeSpan.FromMinutes(30);

    private readonly AdjustableClock clock = new();
    private readonly SessionStore store;

    public SessionStoreListingTests()
    {
        store = new SessionStore(new SessionLifetimes(Idle, Absolute), clock);
    }

    [Fact]
    public void TheListHoldsTheSessionsOfThePersonOnlyWithTheStartTheLastActivityAndTheCurrentOne()
    {
        var first = store.Open(7);
        clock.Advance(TimeSpan.FromMinutes(2));
        var second = store.Open(7);
        _ = store.Open(8);
        clock.Advance(TimeSpan.FromMinutes(1));
        _ = store.Resolve(first);

        var listed = store.ListOf(7, second);

        Assert.Equal(2, listed.Count);
        Assert.Equal([false, true], listed.Select(session => session.Current));
        Assert.Equal(clock.GetUtcNow() - TimeSpan.FromMinutes(3), listed[0].Start);
        Assert.Equal(clock.GetUtcNow(), listed[0].LastActivity);
        Assert.Equal(clock.GetUtcNow() - TimeSpan.FromMinutes(1), listed[1].Start);
    }

    [Fact]
    public void NoSessionIsMarkedCurrentWhenTheAskerIsNotAmongThem()
    {
        _ = store.Open(7);

        Assert.DoesNotContain(store.ListOf(7, null), session => session.Current);
    }

    [Fact]
    public void TheListingIdentifierIsNeverTheSessionIdentifierAndNeverRepeats()
    {
        var first = store.Open(7);
        var second = store.Open(7);

        var ids = store.ListOf(7, first).Select(session => session.ListingId).ToList();

        Assert.Equal(2, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(first, ids);
        Assert.DoesNotContain(second, ids);
        Assert.All(ids, id => Assert.Equal(64, id.Length));
    }

    [Fact]
    public void TheListingIdentifierOpensNothing()
    {
        var identifier = store.Open(7);
        var listing = store.ListOf(7, identifier)[0].ListingId;

        Assert.Null(store.Resolve(listing));
    }

    [Fact]
    public void ReadingTheListDoesNotRenewTheSession()
    {
        var identifier = store.Open(7);
        clock.Advance(Idle - TimeSpan.FromSeconds(1));

        _ = store.ListOf(7, identifier);
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Null(store.Resolve(identifier));
    }

    [Fact]
    public void ASessionThatPassedALimitIsNotListed()
    {
        var old = store.Open(7);
        clock.Advance(Idle + TimeSpan.FromSeconds(1));
        var recent = store.Open(7);

        var listed = store.ListOf(7, recent);

        _ = Assert.Single(listed);
        Assert.Null(store.Resolve(old));
    }

    [Fact]
    public void EndingASessionByItsListingIdentifierMakesTheServerRefuseItAtOnce()
    {
        var kept = store.Open(7);
        var ended = store.Open(7);
        var listing = store.ListOf(7, kept).Single(session => !session.Current).ListingId;

        var closed = store.CloseListed(7, listing);

        Assert.True(closed);
        Assert.Equal(7, store.Resolve(kept));
        Assert.Null(store.Resolve(ended));
        _ = Assert.Single(store.ListOf(7, kept));
    }

    [Fact]
    public void AnotherPersonsListingIdentifierEndsNothing()
    {
        var mine = store.Open(7);
        var theirs = store.Open(8);
        var theirListing = store.ListOf(8, theirs)[0].ListingId;

        var closed = store.CloseListed(7, theirListing);

        Assert.False(closed);
        Assert.Equal((7L, 8L), (store.Resolve(mine)!.Value, store.Resolve(theirs)!.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-listing")]
    public void AnUnknownOrMissingListingIdentifierEndsNothing(string? listing)
    {
        var identifier = store.Open(7);

        Assert.False(store.CloseListed(7, listing));
        Assert.Equal(7, store.Resolve(identifier));
    }

    [Fact]
    public void TheSessionIdentifierIsNotAListingIdentifier()
    {
        var identifier = store.Open(7);

        Assert.False(store.CloseListed(7, identifier));
        Assert.Equal(7, store.Resolve(identifier));
    }

    [Fact]
    public void EndingTheOthersKeepsTheOneGivenAndCountsWhatItEnded()
    {
        var kept = store.Open(7);
        var second = store.Open(7);
        var third = store.Open(7);
        var other = store.Open(8);

        var ended = store.CloseOthersOf(7, kept);

        Assert.Equal(2, ended);
        Assert.Equal(7, store.Resolve(kept));
        Assert.Null(store.Resolve(second));
        Assert.Null(store.Resolve(third));
        Assert.Equal(8, store.Resolve(other));
    }

    [Fact]
    public void EndingTheOthersOfAPersonWithOnlyTheCurrentOneEndsNone()
    {
        var kept = store.Open(7);

        Assert.Equal(0, store.CloseOthersOf(7, kept));
        Assert.Equal(7, store.Resolve(kept));
    }
}
