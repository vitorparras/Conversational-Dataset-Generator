namespace Core.Host.Analysis;

/// <summary>An analysis that concluded, the only result that may ever be kept: how many occurrences, of each kind, and under which model; never an offset.</summary>
/// <param name="Reference">The opaque reference of the analysis.</param>
/// <param name="Occurrences">How many occurrences, of every kind together; zero when nothing was found.</param>
/// <param name="Kinds">How many occurrences of each kind, the kind written exactly as the recognizer wrote it.</param>
/// <param name="ModelVersion">The model behind the analysis; null when the recognizer has none.</param>
internal sealed record ConcludedAnalysis(Guid Reference, int Occurrences, IReadOnlyDictionary<string, int> Kinds, string? ModelVersion);
