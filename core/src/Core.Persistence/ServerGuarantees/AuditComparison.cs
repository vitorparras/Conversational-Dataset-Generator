using Core.Persistence.Custody;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Sets what the server saw against what the application recorded, by role, table and step, for the tables of <see cref="AuditMap"/>.
/// A difference is information and not noise: the server seeing more than the chain holds is a path that wrote without recording, and the chain
/// holding more than the server saw is a log that lost a line or an event that was not made by the application.
/// </summary>
public static class AuditComparison
{
    /// <summary>The differences, in the order of the map and then of the role; none when the two layers agree.</summary>
    public static IReadOnlyList<AuditDifference> Compare(
        IReadOnlyList<ServerAuditEntry> seen, string applicationRole, IReadOnlyDictionary<CustodyStep, int> recorded)
    {
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(applicationRole);
        ArgumentNullException.ThrowIfNull(recorded);
        return [.. AuditMap.Expectations.SelectMany(expectation => ForTable(expectation, seen, applicationRole, recorded))];
    }

    private static IEnumerable<AuditDifference> ForTable(
        AuditExpectation expectation, IEnumerable<ServerAuditEntry> seen, string applicationRole, IReadOnlyDictionary<CustodyStep, int> recorded)
    {
        var matching = seen.Where(entry => entry.ObjectName == expectation.Table && expectation.Commands.Contains(entry.Command, StringComparer.Ordinal));
        var accounted = expectation.Steps.Sum(step => recorded.GetValueOrDefault(step));
        var roles = matching.Select(entry => entry.Role).Append(applicationRole).Distinct(StringComparer.Ordinal);
        return roles
            .Select(role => (Role: role, Seen: matching.Count(entry => entry.Role == role), Recorded: role == applicationRole ? accounted : 0))
            .Where(count => count.Seen != count.Recorded)
            .Select(count => new AuditDifference(
                count.Role, expectation.Table, expectation.Commands, expectation.Steps, count.Seen, count.Recorded, Known(expectation, count.Role == applicationRole, count.Seen, count.Recorded)));
    }

    /// <summary>The known gap only explains the application's role being seen more than recorded; any other role, or the chain holding more, is new.</summary>
    private static string? Known(AuditExpectation expectation, bool applicationRole, int seen, int recorded) =>
        applicationRole && seen > recorded ? expectation.KnownGap : null;
}
