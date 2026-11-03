namespace Core.Host;

/// <summary>How often the chain is verified, as the configuration in force declares; a verification older than twice this is late.</summary>
/// <param name="Interval">The time from the end of one verification to the start of the next.</param>
internal sealed record VerificationSchedule(TimeSpan Interval)
{
    private const int LateAfterIntervals = 2;

    /// <summary>Whether a verification is late: none recorded, or the last one older than twice the interval, so that "never ran" and "stopped running" do not look like "passed".</summary>
    internal bool Overdue(DateTimeOffset? lastRecordedAt, DateTimeOffset now) =>
        lastRecordedAt is not { } recorded || now - recorded > Interval * LateAfterIntervals;
}
