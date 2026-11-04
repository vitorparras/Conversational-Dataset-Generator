using Core.Persistence.Custody.Verification;
using Core.Persistence.ServerGuarantees;

namespace Core.Host.Endpoints;

/// <summary>
/// The last conference of the guarantees: when, whether every one held, which were conferred and which were missing, by name, and whether it is late. With none recorded it is late, not a pass.
/// </summary>
internal sealed record GuaranteesShown(DateTimeOffset? RecordedAt, bool? Passed, IEnumerable<string> Checked, IEnumerable<string> Missing, bool Overdue)
{
    internal static GuaranteesShown Of(RecordedGuarantees? last, bool overdue) =>
        new(last?.RecordedAt, last?.Passed, Named(last is null ? 0 : -1), Named(last?.MissingMask ?? 0), overdue);

    private static IEnumerable<string> Named(long mask) =>
        Enum.GetValues<GuaranteeKinds>().Where(code => code != GuaranteeKinds.None && ((long)code & mask) != 0).Select(code => code.ToString());
}
