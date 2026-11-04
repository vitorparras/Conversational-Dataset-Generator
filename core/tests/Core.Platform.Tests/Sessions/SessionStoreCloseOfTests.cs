using Core.Platform.Sessions;

namespace Core.Platform.Tests.Sessions;

/// <summary>Ending every session of one person (issue 65, part 2): the identifiers they hold are refused at once, and nobody else's session is touched.</summary>
public sealed class SessionStoreCloseOfTests
{
    private readonly SessionStore store = new(new SessionLifetimes(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30)), new AdjustableClock());

    [Fact]
    public void EverySessionOfThePersonIsRefusedAfterwardsAndTheOthersStay()
    {
        var first = store.Open(7);
        var second = store.Open(7);
        var other = store.Open(8);

        store.CloseOf(7);

        Assert.Equal((null, null, 8), (store.Resolve(first), store.Resolve(second), store.Resolve(other)));
    }

    [Fact]
    public void APersonWithNoSessionLeavesTheStoreAsItWas()
    {
        var other = store.Open(8);

        store.CloseOf(7);

        Assert.Equal(8, store.Resolve(other));
    }
}
