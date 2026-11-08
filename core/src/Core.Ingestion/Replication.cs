namespace Core.Ingestion;

/// <summary>
/// The criterion of the replicated content (decision 0024), as a pure function: a message is replicated when its text is equal to that of another, is at least as long as the minimum and comes from
/// at least as many distinct senders as the minimum, the three together and none alone. The comparison is over the keyed digests, so it reaches the whole domain, the messages that came before in other
/// batches among them, and nothing is in clear. It only marks: no message is removed, merged or changed, and every message of a replicated text is marked, the first occurrence included.
/// </summary>
public static class Replication
{
    /// <summary>The marks that the arrival of messages brings, to the ones that arrive and to the ones already kept whose text they make replicated.</summary>
    /// <param name="rule">The two values of the criterion.</param>
    /// <param name="known">The messages already kept in the domain, in the order they came; the first of a text is the first of these.</param>
    /// <param name="arriving">The messages that arrive, in any order: their order in the batch decides nothing, and they follow one another by instant.</param>
    public static IReadOnlyList<ReplicationMark> Marks(ReplicationRule rule, IReadOnlyList<ReplicationOccurrence> known, IReadOnlyList<ReplicationOccurrence> arriving)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(arriving);
        var inOrder = known.Concat(Settled(arriving)).ToList();
        var arrivingPlaces = arriving.Select(message => message.Place).ToHashSet();
        return inOrder
            .Where(message => message.Length >= rule.MinLength)
            .GroupBy(message => message.Text, StringComparer.Ordinal)
            .Where(group => group.Select(message => message.Sender).Distinct(StringComparer.Ordinal).Count() >= rule.MinSenders && group.Any(message => arrivingPlaces.Contains(message.Place)))
            .SelectMany(group => group.Select(message => new ReplicationMark(message.Place, group.First().Place)))
            .OrderBy(mark => mark.First).ThenBy(mark => mark.Of)
            .ToList();
    }

    private static IEnumerable<ReplicationOccurrence> Settled(IReadOnlyList<ReplicationOccurrence> arriving) =>
        arriving.OrderBy(message => message.Instant).ThenBy(message => message.Text, StringComparer.Ordinal).ThenBy(message => message.Sender, StringComparer.Ordinal).ThenBy(message => message.Place);
}
