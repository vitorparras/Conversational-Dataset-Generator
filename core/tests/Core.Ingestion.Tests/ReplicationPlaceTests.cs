namespace Core.Ingestion.Tests;

/// <summary>The order of the places a message is kept in: by item and then by position, with no place before every place.</summary>
public sealed class ReplicationPlaceTests
{
    private static readonly ReplicationPlace Early = new(1, 5);
    private static readonly ReplicationPlace Later = new(1, 6);
    private static readonly ReplicationPlace NextItem = new(2, 0);

    public static TheoryData<ReplicationPlace?, ReplicationPlace?, bool, bool, bool, bool> Pairs => new()
    {
        { null, null, false, true, false, true },
        { null, Early, true, true, false, false },
        { Early, null, false, false, true, true },
        { Early, Later, true, true, false, false },
        { Later, Early, false, false, true, true },
        { Early, new ReplicationPlace(1, 5), false, true, false, true },
        { Later, NextItem, true, true, false, false },
        { NextItem, Later, false, false, true, true },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void PlacesCompareByItemAndThenByPosition(ReplicationPlace? left, ReplicationPlace? right, bool before, bool notAfter, bool after, bool notBefore) =>
        Assert.Equal((before, notAfter, after, notBefore), (left < right, left <= right, left > right, left >= right));

    [Fact]
    public void EveryPlaceComesAfterNoPlace() => Assert.Equal((1, 0, -1), (Early.CompareTo(null), Early.CompareTo(new ReplicationPlace(1, 5)), Early.CompareTo(NextItem)));
}
