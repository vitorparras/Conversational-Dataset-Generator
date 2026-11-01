using Core.Persistence.Custody;

namespace Core.Persistence.ServerGuarantees;

/// <summary>The commands the server must have seen on a table for the application's role, and the steps of the chain that account for them.</summary>
/// <param name="Table">The table, with its schema, as the audit line names it.</param>
/// <param name="Commands">The commands that count, such as INSERT or SELECT.</param>
/// <param name="Steps">The steps of the chain that account for those commands together.</param>
/// <param name="KnownGap">Why the server is known to see more than the steps account for, or null when it is not.</param>
public sealed record AuditExpectation(string Table, IReadOnlyList<string> Commands, IReadOnlyList<CustodyStep> Steps, string? KnownGap = null);
