using Core.Persistence.ServerGuarantees;

namespace Core.Host;

/// <summary>How often the server's log is compared with the chain, the comparison of an interval of time that is run each time, and where what it finds is said.</summary>
internal sealed record AuditComparisonPlan(
    TimeSpan Interval, Func<DateTimeOffset, DateTimeOffset, CancellationToken, Task<IReadOnlyList<AuditDifference>>> Compare, ILogger Log);
