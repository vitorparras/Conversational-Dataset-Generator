namespace Core.Persistence.Recovery;

/// <summary>The answer of the record of a rehearsal: how it ended, the sequence it took in the chain, and which checks failed when it did not.</summary>
public sealed record RecoveryRecord
{
    private RecoveryRecord(RecoveryOutcome outcome, long sequence, IReadOnlyList<string> failures)
    {
        Outcome = outcome;
        Sequence = sequence;
        Failures = failures;
    }

    /// <summary>How the record ended.</summary>
    public RecoveryOutcome Outcome { get; }

    /// <summary>The sequence of the event written; 0 when nothing was written.</summary>
    public long Sequence { get; }

    /// <summary>What failed, in words, one for each check; empty when the step was written.</summary>
    public IReadOnlyList<string> Failures { get; }

    internal static RecoveryRecord Written(long sequence)
    {
        var none = new List<string>();
        return new RecoveryRecord(RecoveryOutcome.Recorded, sequence, none);
    }

    internal static RecoveryRecord Refused(RecoveryOutcome outcome, IReadOnlyList<string> failures) => new(outcome, 0, failures);

    internal static RecoveryRecord Refused(RecoveryOutcome outcome, string failure)
    {
        var failures = new string[1];
        failures[0] = failure;
        return Refused(outcome, failures);
    }
}
