namespace Core.Host.Tests;

/// <summary>
/// A verification is late when none was recorded or the last is older than twice the interval, so that "never ran" and "stopped running" do not look like "passed"
/// (issue #61, part 2, CA-6).
/// </summary>
public sealed class VerificationScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly VerificationSchedule Schedule = new(TimeSpan.FromMinutes(5));

    [Fact]
    public void NoVerificationRecordedIsLate() => Assert.True(Schedule.Overdue(null, Now));

    [Fact]
    public void VerificationJustRecordedIsNotLate() => Assert.False(Schedule.Overdue(Now, Now));

    [Fact]
    public void VerificationOlderThanOneIntervalButNotTwiceIsNotLate() => Assert.False(Schedule.Overdue(Now - TimeSpan.FromMinutes(9), Now));

    [Fact]
    public void VerificationExactlyTwiceTheIntervalOldIsNotYetLate() => Assert.False(Schedule.Overdue(Now - TimeSpan.FromMinutes(10), Now));

    [Fact]
    public void VerificationOlderThanTwiceTheIntervalIsLate() => Assert.True(Schedule.Overdue(Now - TimeSpan.FromMinutes(10) - TimeSpan.FromTicks(1), Now));

    [Fact]
    public void VerificationRecordedAfterTheHostsMomentIsNotLate() => Assert.False(Schedule.Overdue(Now + TimeSpan.FromMinutes(1), Now));
}
