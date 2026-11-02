using Xunit.Sdk;

namespace Core.Persistence.Tests.Custody;

/// <summary>The oracle the concurrent tests judge the chain by must itself fall to a chain that is broken, or those tests prove nothing.</summary>
public sealed class ChainIntegrityTests
{
    private static readonly byte[] Origin = new byte[32];
    private static readonly DateTimeOffset Moment = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ChainNumberedAndLinkedFromTheOriginHolds() =>
        ChainIntegrity.Holds(Chain(4));

    [Fact]
    public void EmptyChainHolds() =>
        ChainIntegrity.Holds([]);

    [Fact]
    public void ChainWithAGapInTheSequenceFails()
    {
        var rows = Chain(4);

        _ = Assert.ThrowsAny<XunitException>(() => ChainIntegrity.Holds([rows[0], rows[1], rows[3]]));
    }

    [Fact]
    public void ChainThatDoesNotBeginAtTheOriginFails()
    {
        var first = Row(1, [.. Enumerable.Repeat((byte)7, 32)]);

        _ = Assert.ThrowsAny<XunitException>(() => ChainIntegrity.Holds([first]));
    }

    [Fact]
    public void EventThatDoesNotPointAtTheDigestOfTheOneBeforeFails()
    {
        var rows = Chain(3);
        var detached = Row(3, [.. Enumerable.Repeat((byte)9, 32)]);

        _ = Assert.ThrowsAny<XunitException>(() => ChainIntegrity.Holds([rows[0], rows[1], detached]));
    }

    [Fact]
    public void EventChangedAfterItWasDigestedFails()
    {
        var rows = Chain(3);
        var changed = rows[1] with { Step = "person_deactivated" };

        _ = Assert.ThrowsAny<XunitException>(() => ChainIntegrity.Holds([rows[0], changed, rows[2]]));
    }

    [Fact]
    public void TwoSuccessorsOfTheSameLinkFail()
    {
        var rows = Chain(2);
        var twin = Row(3, rows[0].Digest);

        _ = Assert.ThrowsAny<XunitException>(() => ChainIntegrity.Holds([rows[0], rows[1], twin]));
    }

    private static List<EventRow> Chain(int length)
    {
        var rows = new List<EventRow>();
        var previous = Origin;
        foreach (var sequence in Enumerable.Range(1, length))
        {
            var row = Row(sequence, previous);
            rows.Add(row);
            previous = row.Digest;
        }

        return rows;
    }

    private static EventRow Row(long sequence, byte[] previous)
    {
        var row = new EventRow(sequence, Moment.AddTicks(sequence * 10), null, true, "person", 100 + sequence, "person_created", "administrative_act", null, previous, Origin, "app");
        return row with { Digest = CanonicalDigest.Of(row) };
    }
}
