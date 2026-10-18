namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// A canary planted for one production source: that source, where the canary sits as the build prints it, the line of each banned route
/// and, for the Sonar canary, the line of each finding of a rule that is on by default.
/// </summary>
internal sealed record CanarySite(string Source, string CanaryPath, IReadOnlyList<int> RouteLines, IReadOnlyList<int>? DefaultOnLines = null);
