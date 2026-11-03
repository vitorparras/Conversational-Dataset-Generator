namespace Core.Persistence.Tests.Custody;

/// <summary>What a chain read from the table must be whatever was written to it: numbered without a gap, each event pointing at the digest of the one before, every link with one successor.</summary>
internal static class ChainIntegrity
{
    private static readonly byte[] Origin = new byte[32];

    internal static void Holds(IReadOnlyCollection<EventRow> rows)
    {
        Assert.Equal(Enumerable.Range(1, rows.Count).Select(number => (long)number), rows.Select(row => row.Sequence));
        Assert.All(rows.Take(1), first => Assert.Equal(Origin, first.PreviousDigest));
        Assert.All(rows.Zip(rows.Skip(1)), pair => Assert.Equal(pair.First.Digest, pair.Second.PreviousDigest));
        Assert.All(rows, row => Assert.Equal(CanonicalDigest.Of(row), row.Digest));
        Assert.Equal(rows.Count, rows.Select(row => Convert.ToHexString(row.PreviousDigest)).Distinct(StringComparer.Ordinal).Count());
    }
}
