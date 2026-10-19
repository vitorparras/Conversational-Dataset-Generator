namespace Core.Boundary.Http;

/// <summary>What the log keeps of an analysis: the opaque reference, how many occurrences, which model and how long; never an offset.</summary>
/// <param name="Reference">The opaque reference of the request.</param>
/// <param name="Occurrences">How many occurrences the answer has, of every kind together.</param>
/// <param name="ModelVersion">The model behind the answer; null when the service has none.</param>
/// <param name="ElapsedMs">The time the recognition took, in milliseconds.</param>
internal sealed record AnalysisSummary(string Reference, int Occurrences, string? ModelVersion, long ElapsedMs);
