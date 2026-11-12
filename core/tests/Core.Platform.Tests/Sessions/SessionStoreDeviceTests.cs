using Core.Platform.Sessions;

namespace Core.Platform.Tests.Sessions;

/// <summary>The description of the device lives with the session in memory only (issue 758): it is listed while the session is open and goes with it however the session ends.</summary>
public sealed class SessionStoreDeviceTests
{
    private const string Firefox = "Firefox no Windows";
    private const string Chrome = "Chrome no Android";

    private readonly SessionStore store = new(new SessionLifetimes(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30)), new AdjustableClock());

    [Fact]
    public void EachSessionListsItsOwnDeviceAndOneOpenedWithoutAnyIsNotIdentified()
    {
        var first = store.Open(7, Firefox);
        _ = store.Open(7, Chrome);
        _ = store.Open(7);

        var listed = store.ListOf(7, first);

        Assert.Equal([Firefox, Chrome, SessionDevice.Unknown], listed.Select(session => session.Device));
    }

    [Fact]
    public void AfterClosingThroughAnyWayTheDeviceIsInNoListing()
    {
        var closed = store.Open(7, Firefox);
        var listed = store.Open(7, Firefox);
        var others = store.Open(7, Firefox);
        var all = store.Open(8, Firefox);
        var kept = store.Open(7, Chrome);

        store.Close(closed);
        _ = store.CloseListed(7, store.ListOf(7, kept).First(session => !session.Current).ListingId);
        _ = store.CloseOthersOf(7, kept);
        _ = store.CloseOf(8);

        Assert.Equal([Chrome], store.ListOf(7, kept).Select(session => session.Device));
        Assert.Empty(store.ListOf(8, all));
        Assert.Null(store.Resolve(listed));
        Assert.Null(store.Resolve(others));
    }
}
