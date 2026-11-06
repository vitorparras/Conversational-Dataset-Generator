namespace Core.Ingestion;

/// <summary>
/// The order of a conversation, which only the content gives: the moments, and where two messages have the same moment, the answer of one to the other, transitively.
/// The place of a message in the file never decides, so the same messages in any order of the file give the same conversation or the same refusal.
/// </summary>
internal static class ConversationOrder
{
    /// <summary>The answers that cannot be in a conversation: an answer with a moment before the message it answers, and every answer that belongs to a cycle of answers.</summary>
    internal static List<Finding> Incoherent(ReadMaterial read)
    {
        var found = new List<Finding>();
        for (var position = 0; position < read.Units.Count; position++)
        {
            if (read.Units[position].RepliesTo is { } to && (read.Units[position].Instant < read.Units[to].Instant || Reaches(read, position, position)))
            {
                found.Add(new Finding(position, MaterialField.RepliesTo));
            }
        }

        return found;
    }

    /// <summary>
    /// The messages that share a moment with another and that the answers do not put in a sequence: every message of such a group,
    /// as nothing in the content tells which comes first.
    /// </summary>
    internal static List<Finding> Undecided(ReadMaterial read)
    {
        var sharing = read.Units.Select((unit, position) => (unit.Instant, position)).GroupBy(pair => pair.Instant).Where(group => group.Count() > 1);
        var undecided = sharing.Where(group => !InSequence(read, group.Select(pair => pair.position)));
        var found = new List<Finding>();
        foreach (var (_, position) in undecided.SelectMany(group => group))
        {
            found.Add(new Finding(position, MaterialField.Instant));
        }

        found.Sort((left, right) => Nullable.Compare(left.Unit, right.Unit));
        return found;
    }

    /// <summary>The positions of the units in the order of the conversation: by moment, and within a moment by the answers, the one answered first.</summary>
    internal static List<int> Sequence(ReadMaterial read)
    {
        var positions = new List<int>(Enumerable.Range(0, read.Units.Count));
        positions.Sort((left, right) => Compare(read, left, right));
        return positions;
    }

    private static int Compare(ReadMaterial read, int left, int right)
    {
        var byMoment = Nullable.Compare(read.Units[left].Instant, read.Units[right].Instant);
        return byMoment != 0 ? byMoment : Depth(read, left).CompareTo(Depth(read, right));
    }

    /// <summary>Whether every pair of the positions is a message and one of its answers, near or far.</summary>
    private static bool InSequence(ReadMaterial read, IEnumerable<int> positions) =>
        positions.All(left => positions.All(right => right == left || Reaches(read, left, right) || Reaches(read, right, left)));

    /// <summary>Whether following the answers from <paramref name="from" /> reaches <paramref name="target" />, in no more steps than there are units.</summary>
    private static bool Reaches(ReadMaterial read, int from, int target)
    {
        var at = from;
        for (var step = 0; step < read.Units.Count && read.Units[at].RepliesTo is { } to; step++)
        {
            if (to == target)
            {
                return true;
            }

            at = to;
        }

        return false;
    }

    /// <summary>How many messages are answered, one after the other, before one that answers none; an answer is always deeper than the message it answers.</summary>
    private static int Depth(ReadMaterial read, int position)
    {
        var depth = 0;
        for (var at = position; read.Units[at].RepliesTo is { } to; at = to)
        {
            depth++;
        }

        return depth;
    }
}
