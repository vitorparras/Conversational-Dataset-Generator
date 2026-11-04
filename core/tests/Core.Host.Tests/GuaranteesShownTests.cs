using Core.Host.Endpoints;
using Core.Persistence.Custody.Verification;

namespace Core.Host.Tests;

/// <summary>The answer about the guarantees (issue #68, CA-5): with none recorded it is late and confers nothing, which is not a pass.</summary>
public sealed class GuaranteesShownTests
{
    [Fact]
    public void NoConferenceRecordedIsLateWithNothingConferredAndNoVerdict()
    {
        var shown = GuaranteesShown.Of(last: null, overdue: true);

        Assert.Equal((null, null, true), (shown.RecordedAt, shown.Passed, shown.Overdue));
        Assert.Empty(shown.Checked);
        Assert.Empty(shown.Missing);
    }

    [Fact]
    public void ConferenceRecordedNamesEveryGuaranteeConferredAndOnlyTheOnesMissing()
    {
        var at = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

        var shown = GuaranteesShown.Of(new RecordedGuarantees(at, Passed: false, MissingMask: 2 | 8), overdue: false);

        Assert.Equal(["Privilege", "RowPolicy", "ColumnRevoke", "Immutability", "ProtectedColumns", "EncryptedSample"], shown.Checked);
        Assert.Equal(["RowPolicy", "Immutability"], shown.Missing);
        Assert.Equal((at, false, false), (shown.RecordedAt, shown.Passed, shown.Overdue));
    }
}
