namespace Core.Host.Submissions;

/// <summary>Whether the ingestion takes material in: open, or blocked by the code of what is missing (decision 0014).</summary>
/// <param name="Open">Whether the gate is open.</param>
/// <param name="Code">The code of what keeps it closed; null when it is open.</param>
internal sealed record SubmissionGateStatus(bool Open, string? Code);
