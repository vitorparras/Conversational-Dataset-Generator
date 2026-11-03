namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// What the comparison of an interval found: the differences between the layers, and the log files whose last line the server's fall cut short,
/// which are a notice and no difference.
/// </summary>
/// <param name="Differences">The differences found, the known ones among them; empty when the layers agree.</param>
/// <param name="CutOffFiles">The names of the log files that end in a line without a line break and are not the newest.</param>
public sealed record AuditCompared(IReadOnlyList<AuditDifference> Differences, IReadOnlyList<string> CutOffFiles);
