using Core.Persistence.Custody;

namespace Core.Persistence.ServerGuarantees;

/// <summary>Where the server's log and the chain disagree for one role and one table: how many commands the server saw and how many events the steps recorded.</summary>
/// <param name="Role">The role the server saw the commands run as.</param>
/// <param name="Table">The table, with its schema.</param>
/// <param name="Commands">The commands counted.</param>
/// <param name="Steps">The steps the chain would have recorded them under.</param>
/// <param name="Seen">The commands the server saw.</param>
/// <param name="Recorded">The events the chain holds for those steps; always zero for a role other than the application's.</param>
/// <param name="KnownGap">The reason, when the difference is the known one and not a new one.</param>
public sealed record AuditDifference(
    string Role, string Table, IReadOnlyList<string> Commands, IReadOnlyList<CustodyStep> Steps, int Seen, int Recorded, string? KnownGap);
