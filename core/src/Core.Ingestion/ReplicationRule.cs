namespace Core.Ingestion;

/// <summary>
/// The two values of the criterion of the replicated content (decision 0024) that the configuration of the installation holds, the same for every domain:
/// the smallest text that can be replicated and how many distinct senders
/// a text has to come from. The third part of the criterion, text equal, has no value to configure.
/// </summary>
public sealed record ReplicationRule
{
    /// <summary>The fewest distinct senders the criterion can ask for: one sender repeating itself is behaviour and not replication.</summary>
    public static int FewestSenders { get; } = 2;

    /// <summary>The criterion with its two values.</summary>
    /// <param name="minLength">The smallest length, in code points, a text can have to be replicated: at least one.</param>
    /// <param name="minSenders">How many distinct senders the same text comes from: at least two, since one sender repeating itself is behaviour and not replication.</param>
    public ReplicationRule(int minLength, int minSenders)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minSenders, FewestSenders);
        MinLength = minLength;
        MinSenders = minSenders;
    }

    /// <summary>The smallest length, in code points, a text can have to be replicated.</summary>
    public int MinLength { get; }

    /// <summary>How many distinct senders the same text comes from.</summary>
    public int MinSenders { get; }
}
